using smart_pet_care_api.Modules.PetModule.Repository;
using smart_pet_care_api.Modules.ReminderModule.DTOs.Requests;
using smart_pet_care_api.Modules.ReminderModule.DTOs.Responses;
using smart_pet_care_api.Modules.ReminderModule.Mapper;
using smart_pet_care_api.Modules.ReminderModule.Repository;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.ReminderModule.Domain
{
    public class ReminderService : IReminderService
    {
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

            ValidateMode(dto.RepeatType, dto.Days, dto.Date);

            var (firstTrigger, timeOfDayUtc) = ComputeSchedule(
                dto.RepeatType, dto.Days, dto.Date, dto.Time, dto.UtcOffsetMinutes, DateTime.UtcNow);

            var reminder = ReminderMapper.ToEntity(dto, firstTrigger, timeOfDayUtc);
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
                || dto.Date.HasValue || dto.Time.HasValue || dto.UtcOffsetMinutes.HasValue;

            if (touchesSchedule)
            {
                var repeatType = dto.RepeatType ?? reminder.RepeatType;
                var offset = dto.UtcOffsetMinutes ?? reminder.UtcOffsetMinutes;

                var usesDate = repeatType is RepeatType.Monthly or RepeatType.Once;

                // Reject request fields that don't belong to the (target) mode; stale entity
                // fields left over from a mode switch are cleared silently instead.
                if (!usesDate && dto.Date.HasValue)
                    throw new InvalidOperationException($"{repeatType} reminders must not include a date.");
                if (repeatType != RepeatType.Weekly && dto.Days is { Length: > 0 })
                    throw new InvalidOperationException($"{repeatType} reminders must not include days.");

                var days = repeatType == RepeatType.Weekly ? (dto.Days ?? reminder.Days) : [];
                DateOnly? date = usesDate ? (dto.Date ?? reminder.Date) : null;

                ValidateMode(repeatType, days, date);

                var localTime = dto.Time ?? TimeOnly.FromTimeSpan(reminder.TimeOfDay)
                    .Add(TimeSpan.FromMinutes(offset));

                var (trigger, timeOfDayUtc) = ComputeSchedule(
                    repeatType, days, date, localTime, offset, DateTime.UtcNow);

                reminder.RepeatType = repeatType;
                reminder.Days = days;
                reminder.Date = date;
                reminder.UtcOffsetMinutes = offset;
                reminder.TimeOfDay = timeOfDayUtc;
                reminder.StartAt = trigger;
                reminder.NextTriggerAt = trigger;

                // Rescheduling implies the reminder should fire again; without this a
                // Completed/Missed reminder would hold a trigger the scheduler ignores.
                if (!dto.Status.HasValue)
                    reminder.Status = ReminderStatus.Active;
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
        /// Days are stored as the user's local weekdays while TimeOfDay is UTC; when the
        /// local→UTC conversion crosses midnight the occurrence falls on the adjacent UTC weekday.
        /// </summary>
        internal static DaysOfWeek[] ToUtcDays(DaysOfWeek[] localDays, TimeSpan timeOfDayUtc, int offsetMinutes)
        {
            var localMinutes = timeOfDayUtc.TotalMinutes + offsetMinutes;
            var shift = localMinutes >= 1440 ? -1 : localMinutes < 0 ? 1 : 0;

            return shift == 0
                ? localDays
                : localDays.Select(d => (DaysOfWeek)(((int)d + shift + 7) % 7)).ToArray();
        }

        internal static DateTime? ComputeNextTrigger(DaysOfWeek[] days, TimeSpan time, DateTime after)
        {
            if (days.Length == 0) return null;

            return days
                .Select(day => NextOccurrence(day, time, after))
                .OrderBy(d => d)
                .Cast<DateTime?>()
                .First();
        }

        private static (DateTime trigger, TimeSpan timeOfDayUtc) ComputeSchedule(
            RepeatType repeatType, DaysOfWeek[] days, DateOnly? date, TimeOnly localTime, int offsetMinutes, DateTime nowUtc)
        {
            switch (repeatType)
            {
                case RepeatType.Weekly:
                    {
                        // No "time already passed today" guard: ComputeNextTrigger rolls a same-day-but-past
                        // time forward to next week automatically.
                        var timeUtc = localTime.Add(TimeSpan.FromMinutes(-offsetMinutes)).ToTimeSpan();
                        var utcDays = ToUtcDays(days, timeUtc, offsetMinutes);
                        var trigger = ComputeNextTrigger(utcDays, timeUtc, nowUtc)
                            ?? throw new InvalidOperationException("Could not compute a valid trigger time");
                        return (trigger, timeUtc);
                    }
                case RepeatType.Daily:
                    {
                        var timeUtc = localTime.Add(TimeSpan.FromMinutes(-offsetMinutes)).ToTimeSpan();
                        return (ComputeNextDaily(timeUtc, nowUtc), timeUtc);
                    }
                case RepeatType.Monthly:
                    {
                        var trigger = ComputeNextMonthly(date!.Value, localTime, offsetMinutes, nowUtc);
                        return (trigger, trigger.TimeOfDay);
                    }
                case RepeatType.Once:
                    {
                        var trigger = date!.Value.ToDateTime(localTime, DateTimeKind.Utc).AddMinutes(-offsetMinutes);
                        if (trigger <= nowUtc)
                            throw new InvalidOperationException("Date must be in the future.");
                        return (trigger, trigger.TimeOfDay);
                    }
                default:
                    throw new InvalidOperationException("Unknown repeat type.");
            }
        }

        private static void ValidateMode(RepeatType repeatType, DaysOfWeek[] days, DateOnly? date)
        {
            switch (repeatType)
            {
                case RepeatType.Weekly:
                    if (days.Length == 0)
                        throw new InvalidOperationException("Weekly reminders require at least one day.");
                    if (date.HasValue)
                        throw new InvalidOperationException("Weekly reminders must not include a date.");
                    break;
                case RepeatType.Daily:
                    if (days.Length > 0)
                        throw new InvalidOperationException("Daily reminders must not include days.");
                    if (date.HasValue)
                        throw new InvalidOperationException("Daily reminders must not include a date.");
                    break;
                case RepeatType.Monthly:
                case RepeatType.Once:
                    if (!date.HasValue)
                        throw new InvalidOperationException($"{repeatType} reminders require a date.");
                    if (days.Length > 0)
                        throw new InvalidOperationException($"{repeatType} reminders must not include days.");
                    break;
                default:
                    throw new InvalidOperationException("Unknown repeat type.");
            }
        }

        internal static DateTime ComputeNextDaily(TimeSpan timeOfDayUtc, DateTime afterUtc)
        {
            var candidate = afterUtc.Date + timeOfDayUtc;
            return candidate <= afterUtc ? candidate.AddDays(1) : candidate;
        }

        internal static DateTime ComputeNextMonthly(DateOnly anchor, TimeOnly localTime, int offsetMinutes, DateTime afterUtc)
        {
            var afterLocal = afterUtc.AddMinutes(offsetMinutes);

            var candidate = BuildMonthlyLocal(afterLocal.Year, afterLocal.Month, anchor.Day, localTime);
            if (candidate <= afterLocal)
            {
                var nextMonth = new DateTime(afterLocal.Year, afterLocal.Month, 1).AddMonths(1);
                candidate = BuildMonthlyLocal(nextMonth.Year, nextMonth.Month, anchor.Day, localTime);
            }

            return DateTime.SpecifyKind(candidate.AddMinutes(-offsetMinutes), DateTimeKind.Utc);
        }

        private static DateTime BuildMonthlyLocal(int year, int month, int dayOfMonth, TimeOnly localTime)
        {
            var day = Math.Min(dayOfMonth, DateTime.DaysInMonth(year, month));
            return new DateTime(year, month, day).Add(localTime.ToTimeSpan());
        }

        private static DateTime NextOccurrence(DaysOfWeek day, TimeSpan time, DateTime after)
        {
            var daysUntil = ((int)day - (int)after.DayOfWeek + 7) % 7;
            if (daysUntil == 0 && after.TimeOfDay > time)
                daysUntil = 7;
            return after.Date.AddDays(daysUntil) + time;
        }
    }
}
