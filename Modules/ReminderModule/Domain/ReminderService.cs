using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.PetModule.Repository;
using smart_pet_care_api.Modules.ReminderModule.DTOs.Requests;
using smart_pet_care_api.Modules.ReminderModule.DTOs.Responses;
using smart_pet_care_api.Modules.ReminderModule.Mapper;
using smart_pet_care_api.Modules.ReminderModule.Repository;
using static smart_pet_care_api.Models.Enums;
using static smart_pet_care_api.Modules.ReminderModule.Domain.ReminderScheduleCalculator;

namespace smart_pet_care_api.Modules.ReminderModule.Domain
{
    public class ReminderService : IReminderService
    {
        /// <summary>Furthest ahead occurrences may be projected, to bound a daily rule's expansion.</summary>
        private static readonly TimeSpan MaxHorizon = TimeSpan.FromDays(90);

        private const int MaxProjectedPerReminder = 60;

        private readonly IReminderRepository _reminderRepo;
        private readonly IPetRepository _petRepo;

        public ReminderService(IReminderRepository repo, IPetRepository petRepo)
        {
            _reminderRepo = repo;
            _petRepo = petRepo;
        }

        public async Task<IReadOnlyList<ReminderResponseDto>> GetByUserIdAsync(Guid userId)
        {
            var pets = await _petRepo.GetByUserIdAsync(userId);
            var petIds = pets.Select(p => p.Id);
            var reminders = await _reminderRepo.GetByPetIdsAsync(petIds);
            return reminders.Select(r => r.ToDto()).ToList();
        }

        public async Task<IReadOnlyList<ReminderResponseDto>> GetByPetIdAsync(Guid petId, Guid userId)
        {
            if (!await _petRepo.ExistsForUserAsync(petId, userId))
                throw new InvalidOperationException("Pet not found");

            var reminders = await _reminderRepo.GetByPetIdAsync(petId);
            return reminders.Select(r => r.ToDto()).ToList();
        }

        public async Task<ReminderResponseDto?> GetByIdAsync(Guid id, Guid userId)
        {
            var reminder = await _reminderRepo.GetByIdAsync(id);
            if (reminder == null) return null;
            if (!await _petRepo.ExistsForUserAsync(reminder.PetId, userId)) return null;
            return reminder.ToDto();
        }

        public async Task<ReminderResponseDto> CreateAsync(CreateReminderDto dto, Guid userId)
        {
            var pet = await _petRepo.GetByIdAndUserIdAsync(dto.PetId, userId)
                ?? throw new InvalidOperationException("Pet not found");

            if (dto.EndAt.HasValue && dto.EndAt.Value <= DateTime.UtcNow)
                throw new InvalidOperationException("EndAt must be in the future");

            var strategy = ResolveStrategy(dto.Type, dto.RepeatType, dto.RecalcStrategy);
            ValidateSchedule(dto.RepeatType, dto.IntervalN, strategy, dto.Days, dto.Date);

            var (firstTrigger, timeOfDayUtc) = ComputeSchedule(
                dto.RepeatType, dto.IntervalN, strategy, dto.Days, dto.Date,
                dto.Time, dto.UtcOffsetMinutes, DateTime.UtcNow);

            var reminder = ReminderMapper.ToEntity(dto, firstTrigger, timeOfDayUtc, strategy);
            await _reminderRepo.AddAsync(reminder);
            await _reminderRepo.SaveChangesAsync();

            var response = reminder.ToDto();
            response.PetSpecies = pet.Species;
            return response;
        }

        public async Task<ReminderResponseDto> UpdateAsync(Guid id, PatchReminderDto dto, Guid userId)
        {
            var reminder = await _reminderRepo.GetByIdAsync(id)
                ?? throw new InvalidOperationException("Reminder not found");

            if (!await _petRepo.ExistsForUserAsync(reminder.PetId, userId))
                throw new InvalidOperationException("Reminder not found");

            if (dto.EndAt.HasValue && dto.EndAt.Value <= DateTime.UtcNow)
                throw new InvalidOperationException("EndAt must be in the future");

            reminder.PatchEntity(dto);

            var touchesSchedule = dto.RepeatType.HasValue || dto.Days != null
                || dto.Date.HasValue || dto.Time.HasValue || dto.UtcOffsetMinutes.HasValue
                || dto.IntervalN.HasValue || dto.RecalcStrategy.HasValue;

            if (touchesSchedule)
            {
                var repeatType = dto.RepeatType ?? reminder.RepeatType;
                var offset = dto.UtcOffsetMinutes ?? reminder.UtcOffsetMinutes;
                var intervalN = dto.IntervalN ?? reminder.IntervalN;
                var strategy = ResolveStrategy(
                    reminder.Type, repeatType, dto.RecalcStrategy ?? reminder.RecalcStrategy);

                var usesDate = repeatType is RepeatType.Monthly or RepeatType.Once;

                // Reject request fields that don't belong to the (target) mode; stale entity
                // fields left over from a mode switch are cleared silently instead.
                if (!usesDate && dto.Date.HasValue)
                    throw new InvalidOperationException($"{repeatType} reminders must not include a date.");
                if (!ReminderMapper.KeepDays(repeatType, strategy) && dto.Days is { Length: > 0 })
                    throw new InvalidOperationException($"{repeatType} reminders must not include days.");

                var days = ReminderMapper.KeepDays(repeatType, strategy) ? (dto.Days ?? reminder.Days) : [];
                DateOnly? date = usesDate ? (dto.Date ?? reminder.Date) : null;

                ValidateSchedule(repeatType, intervalN, strategy, days, date);

                var localTime = dto.Time ?? TimeOnly.FromTimeSpan(reminder.TimeOfDay)
                    .Add(TimeSpan.FromMinutes(offset));

                var (trigger, timeOfDayUtc) = ComputeSchedule(
                    repeatType, intervalN, strategy, days, date, localTime, offset, DateTime.UtcNow);

                reminder.RepeatType = repeatType;
                reminder.IntervalN = intervalN;
                reminder.RecalcStrategy = strategy;
                reminder.Days = days;
                reminder.Date = date;
                reminder.UtcOffsetMinutes = offset;
                reminder.TimeOfDay = timeOfDayUtc;
                reminder.StartAt = trigger;
                reminder.NextTriggerAt = trigger;

                // Editing the schedule restarts the interval, so the anchor follows the new
                // first occurrence; keeping the old one would leave week parity counting from
                // a rule that no longer exists.
                reminder.ScheduleAnchorAt = trigger;

                // Rescheduling implies the reminder should fire again; without this a
                // Completed/Missed reminder would hold a trigger the scheduler ignores.
                if (!dto.Status.HasValue)
                    reminder.Status = ReminderStatus.Active;

                // The pending occurrence was replaced, so whatever was overdue no longer is.
                reminder.OverdueSince = null;
            }

            await _reminderRepo.SaveChangesAsync();
            return reminder.ToDto();
        }

        public async Task DeleteAsync(Guid id, Guid userId)
        {
            var reminder = await _reminderRepo.GetByIdAsync(id)
                ?? throw new InvalidOperationException("Reminder not found");

            if (!await _petRepo.ExistsForUserAsync(reminder.PetId, userId))
                throw new InvalidOperationException("Reminder not found");

            await _reminderRepo.DeleteAsync(reminder);
            await _reminderRepo.SaveChangesAsync();
        }

        public async Task<IReadOnlyList<ReminderRunResponseDto>> GetRunsAsync(Guid reminderId, Guid userId)
        {
            var reminder = await _reminderRepo.GetByIdAsync(reminderId);
            if (reminder == null || !await _petRepo.ExistsForUserAsync(reminder.PetId, userId))
                throw new InvalidOperationException("Reminder not found");

            var runs = await _reminderRepo.GetRunsByReminderIdAsync(reminderId);
            return runs.Select(r => r.ToDto()).ToList();
        }

        public async Task<IReadOnlyList<ReminderRunHistoryDto>> GetRunHistoryAsync(
            Guid petId, Guid userId, DateTime? from, DateTime? to, ReminderType? type)
        {
            if (!await _petRepo.ExistsForUserAsync(petId, userId))
                throw new InvalidOperationException("Pet not found");

            var fromUtc = from is { } f ? ReminderMapper.NormalizeToUtc(f) : (DateTime?)null;
            var toUtc = to is { } t ? ReminderMapper.NormalizeToUtc(t) : (DateTime?)null;

            if (fromUtc.HasValue && toUtc.HasValue && fromUtc > toUtc)
                throw new ArgumentException("From cannot be later than To");

            var rows = await _reminderRepo.GetRunHistoryByPetIdAsync(petId, fromUtc, toUtc, type);
            return rows.Select(row => row.Run.ToHistoryDto(row.Reminder)).ToList();
        }

        /// <summary>
        /// Occurrences in a window: stored runs for what already happened, plus occurrences
        /// projected from the repeat rules for what has not. Projections are never written —
        /// editing a routine would otherwise mean deleting and regenerating everything ahead
        /// of it.
        /// </summary>
        public async Task<IReadOnlyList<ReminderOccurrenceDto>> GetOccurrencesAsync(
            Guid userId, Guid? petId, DateTime from, DateTime to)
        {
            var fromUtc = ReminderMapper.NormalizeToUtc(from);
            var toUtc = ReminderMapper.NormalizeToUtc(to);

            if (fromUtc > toUtc)
                throw new ArgumentException("From cannot be later than To");

            if (toUtc - fromUtc > MaxHorizon)
                throw new ArgumentException($"Window cannot exceed {MaxHorizon.TotalDays:0} days");

            IReadOnlyList<Guid> petIds;
            if (petId is { } single)
            {
                if (!await _petRepo.ExistsForUserAsync(single, userId))
                    throw new InvalidOperationException("Pet not found");
                petIds = [single];
            }
            else
            {
                var pets = await _petRepo.GetByUserIdAsync(userId);
                petIds = pets.Select(p => p.Id).ToList();
            }

            if (petIds.Count == 0) return [];

            var reminders = await _reminderRepo.GetSchedulableByPetIdsAsync(petIds);
            if (reminders.Count == 0) return [];

            var runs = await _reminderRepo.GetRunsByReminderIdsAsync(
                reminders.Select(r => r.Id).ToList(), fromUtc, toUtc);

            var byReminder = reminders.ToDictionary(r => r.Id);
            var occurrences = new List<ReminderOccurrenceDto>();
            var materialised = new HashSet<(Guid, DateTime)>();

            foreach (var run in runs)
            {
                if (!byReminder.TryGetValue(run.ReminderId, out var reminder)) continue;

                materialised.Add((run.ReminderId, run.ScheduledFor));
                occurrences.Add(ToOccurrence(reminder, run.ScheduledFor, run));
            }

            foreach (var reminder in reminders)
                occurrences.AddRange(Project(reminder, fromUtc, toUtc, materialised));

            return occurrences.OrderBy(o => o.ScheduledFor).ToList();
        }

        private static IEnumerable<ReminderOccurrenceDto> Project(
            Reminder reminder, DateTime fromUtc, DateTime toUtc, HashSet<(Guid, DateTime)> materialised)
        {
            if (reminder.NextTriggerAt is not { } next) yield break;

            var plan = PlanFor(reminder);
            var horizon = reminder.EndAt.HasValue && reminder.EndAt < toUtc ? reminder.EndAt.Value : toUtc;

            for (var i = 0; i < MaxProjectedPerReminder && next <= horizon; i++)
            {
                if (next >= fromUtc && materialised.Add((reminder.Id, next)))
                    yield return ToOccurrence(reminder, next, run: null);

                // Past the pending trigger, a completion-driven rule has no knowable schedule:
                // the date after this one depends on when the user actually does it.
                if (reminder.RecalcStrategy != RecalcStrategy.Calendar) yield break;

                if (NextFromCalendar(plan, next) is not { } following) yield break;
                next = following;
            }
        }

        private static ReminderOccurrenceDto ToOccurrence(Reminder reminder, DateTime scheduledFor, ReminderRun? run) => new()
        {
            ReminderId = reminder.Id,
            PetId = reminder.PetId,
            Title = reminder.Title,
            Type = run?.Type ?? reminder.Type,
            ScheduledFor = scheduledFor,
            RunId = run?.Id,
            Status = run?.Status,
            CompletedAt = run?.CompletedAt,
            PerformedAt = run?.PerformedAt,
            IsMaterialized = run is not null,
            IsOverdue = reminder.OverdueSince.HasValue && scheduledFor <= reminder.OverdueSince.Value
        };

        public async Task<ReminderRunResponseDto> AcknowledgeRunAsync(Guid runId, Guid userId)
        {
            var run = await _reminderRepo.GetRunByIdAsync(runId)
                ?? throw new InvalidOperationException("Reminder run not found");

            var reminder = await _reminderRepo.GetByIdAsync(run.ReminderId);
            if (reminder == null || !await _petRepo.ExistsForUserAsync(reminder.PetId, userId))
                throw new InvalidOperationException("Reminder run not found");

            if (run.Status == ReminderRunStatus.Completed)
                throw new InvalidOperationException("Run already acknowledged");

            run.Status = ReminderRunStatus.Completed;
            run.CompletedAt = DateTime.UtcNow;
            run.UpdatedAt = DateTime.UtcNow;

            await _reminderRepo.SaveChangesAsync();
            return run.ToDto();
        }

        /// <summary>
        /// Medical intervals are a safety property, so the recalculation mode for those types
        /// is decided here rather than taken from the request. One-off reminders are exempt
        /// because they have no interval to count from. The effective value comes back in the
        /// response, so a client that asked for something else can see what it got.
        /// </summary>
        internal static RecalcStrategy ResolveStrategy(
            ReminderType type, RepeatType repeatType, RecalcStrategy requested) =>
            repeatType != RepeatType.Once && ReminderTypePolicy.RequiresCompletionDrivenRecalc(type)
                ? RecalcStrategy.FromCompletion
                : requested;

        private static (DateTime trigger, TimeSpan timeOfDayUtc) ComputeSchedule(
            RepeatType repeatType, int intervalN, RecalcStrategy strategy, DaysOfWeek[] days,
            DateOnly? date, TimeOnly localTime, int offsetMinutes, DateTime nowUtc)
        {
            var timeOfDayUtc = localTime.Add(TimeSpan.FromMinutes(-offsetMinutes)).ToTimeSpan();

            if (repeatType == RepeatType.Once)
            {
                var once = DateTime.SpecifyKind(
                    date!.Value.ToDateTime(localTime).AddMinutes(-offsetMinutes), DateTimeKind.Utc);
                if (once <= nowUtc)
                    throw new InvalidOperationException("Date must be in the future.");
                return (once, once.TimeOfDay);
            }

            var plan = new SchedulePlan(
                repeatType, intervalN, days, date, timeOfDayUtc, offsetMinutes, nowUtc, strategy);

            var trigger = FirstTrigger(plan, nowUtc)
                ?? throw new InvalidOperationException("Could not compute a valid trigger time");

            return (trigger, timeOfDayUtc);
        }

        internal static void ValidateSchedule(
            RepeatType repeatType, int intervalN, RecalcStrategy strategy, DaysOfWeek[] days, DateOnly? date)
        {
            if (!Enum.IsDefined(repeatType))
                throw new InvalidOperationException("Unknown repeat type.");
            if (!Enum.IsDefined(strategy))
                throw new InvalidOperationException("Unknown recalculation strategy.");

            var maxInterval = repeatType switch
            {
                RepeatType.Daily => 365,
                RepeatType.Weekly => 52,
                RepeatType.Monthly => 24,
                _ => 1
            };

            if (intervalN < 1 || intervalN > maxInterval)
                throw new InvalidOperationException(
                    $"IntervalN must be between 1 and {maxInterval} for {repeatType} reminders.");

            if (repeatType == RepeatType.Once && strategy != RecalcStrategy.Calendar)
                throw new InvalidOperationException(
                    "Once reminders have no interval to recalculate from.");

            if (strategy == RecalcStrategy.FromCompletionAlignedToWeekday && days.Length == 0)
                throw new InvalidOperationException(
                    "Weekday-aligned recalculation requires at least one day to align to.");

            var daysAllowed = ReminderMapper.KeepDays(repeatType, strategy);

            switch (repeatType)
            {
                case RepeatType.Weekly:
                    if (days.Length == 0)
                        throw new InvalidOperationException("Weekly reminders require at least one day.");
                    if (date.HasValue)
                        throw new InvalidOperationException("Weekly reminders must not include a date.");
                    break;
                case RepeatType.Daily:
                    if (days.Length > 0 && !daysAllowed)
                        throw new InvalidOperationException("Daily reminders must not include days.");
                    if (date.HasValue)
                        throw new InvalidOperationException("Daily reminders must not include a date.");
                    break;
                case RepeatType.Monthly:
                case RepeatType.Once:
                    if (!date.HasValue)
                        throw new InvalidOperationException($"{repeatType} reminders require a date.");
                    if (days.Length > 0 && !daysAllowed)
                        throw new InvalidOperationException($"{repeatType} reminders must not include days.");
                    break;
                default:
                    throw new InvalidOperationException("Unknown repeat type.");
            }
        }
    }
}
