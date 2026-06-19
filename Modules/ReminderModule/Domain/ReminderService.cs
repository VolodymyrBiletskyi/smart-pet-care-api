using Microsoft.EntityFrameworkCore;
using smart_pet_care_api.Data;
using smart_pet_care_api.Modules.ReminderModule.DTOs.Requests;
using smart_pet_care_api.Modules.ReminderModule.DTOs.Responses;
using smart_pet_care_api.Modules.ReminderModule.Mapper;
using smart_pet_care_api.Modules.ReminderModule.Repository;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.ReminderModule.Domain
{
    public class ReminderService : IReminderService
    {
        private readonly IReminderRepository _repo;
        private readonly AppDbContext _db;

        public ReminderService(IReminderRepository repo, AppDbContext db)
        {
            _repo = repo;
            _db = db;
        }

        public async Task<IReadOnlyList<ReminderResponseDto>> GetByUserIdAsync(Guid userId)
        {
            var reminders = await _repo.GetByUserIdAsync(userId);
            return reminders.Select(r => r.ToDto()).ToList();
        }

        public async Task<IReadOnlyList<ReminderResponseDto>> GetByPetIdAsync(Guid petId, Guid userId)
        {
            var reminders = await _repo.GetByPetIdAsync(petId, userId);
            return reminders.Select(r => r.ToDto()).ToList();
        }

        public async Task<ReminderResponseDto?> GetByIdAsync(Guid id, Guid userId)
        {
            var reminder = await _repo.GetByIdAsync(id, userId);
            return reminder?.ToDto();
        }

        public async Task<ReminderResponseDto> CreateAsync(CreateReminderDto dto, Guid userId)
        {
            var petBelongsToUser = await _db.Pets.AnyAsync(p => p.Id == dto.PetId && p.UserId == userId);
            if (!petBelongsToUser)
                throw new InvalidOperationException("Pet not found");

            if (dto.Days.Length == 0)
                throw new InvalidOperationException("At least one day must be specified");

            if (dto.EndAt.HasValue && dto.EndAt.Value <= DateTime.UtcNow)
                throw new InvalidOperationException("EndAt must be in the future");

            var localNow = DateTime.UtcNow.AddMinutes(dto.UtcOffsetMinutes);
            var localDay = (DaysOfWeek)localNow.DayOfWeek;
            if (dto.Days.Contains(localDay) && dto.Time <= TimeOnly.FromDateTime(localNow))
                throw new InvalidOperationException("Reminder time has already passed for today. Choose a future time or a different day.");

            var timeUtc = dto.Time.Add(TimeSpan.FromMinutes(-dto.UtcOffsetMinutes));
            var firstTrigger = ComputeNextTrigger(dto.Days, timeUtc.ToTimeSpan(), DateTime.UtcNow)
                ?? throw new InvalidOperationException("Could not compute a valid trigger time");

            var reminder = ReminderMapper.ToEntity(dto, firstTrigger, timeUtc.ToTimeSpan());
            await _repo.AddAsync(reminder);
            await _repo.SaveChangesAsync();
            return reminder.ToDto();
        }

        public async Task<ReminderResponseDto> UpdateAsync(Guid id, PatchReminderDto dto, Guid userId)
        {
            var reminder = await _repo.GetByIdAsync(id, userId)
                ?? throw new InvalidOperationException("Reminder not found");

            if (dto.EndAt.HasValue && dto.EndAt.Value <= DateTime.UtcNow)
                throw new InvalidOperationException("EndAt must be in the future");

            TimeOnly? timeUtc = null;
            if (dto.Time.HasValue)
            {
                var offset = dto.UtcOffsetMinutes ?? 0;
                var localNow = DateTime.UtcNow.AddMinutes(offset);
                var days = dto.Days ?? reminder.Days;
                var localDay = (DaysOfWeek)localNow.DayOfWeek;
                if (days.Contains(localDay) && dto.Time.Value <= TimeOnly.FromDateTime(localNow))
                    throw new InvalidOperationException("Reminder time has already passed for today. Choose a future time or a different day.");

                timeUtc = dto.Time.Value.Add(TimeSpan.FromMinutes(-offset));
            }

            reminder.PatchEntity(dto, timeUtc);

            if (dto.Days != null || dto.Time.HasValue)
            {
                var days = dto.Days ?? reminder.Days;
                var time = timeUtc.HasValue ? timeUtc.Value.ToTimeSpan() : reminder.TimeOfDay;

                if (days.Length == 0)
                    throw new InvalidOperationException("At least one day must be specified");

                var next = ComputeNextTrigger(days, time, DateTime.UtcNow);
                reminder.NextTriggerAt = next;
                if (next.HasValue) reminder.StartAt = next.Value;
            }

            await _repo.SaveChangesAsync();
            return reminder.ToDto();
        }

        public async Task DeleteAsync(Guid id, Guid userId)
        {
            var reminder = await _repo.GetByIdAsync(id, userId)
                ?? throw new InvalidOperationException("Reminder not found");

            await _repo.DeleteAsync(reminder);
            await _repo.SaveChangesAsync();
        }

        public async Task<IReadOnlyList<ReminderRunResponseDto>> GetRunsAsync(Guid reminderId, Guid userId)
        {
            var runs = await _repo.GetRunsByReminderIdAsync(reminderId, userId);
            return runs.Select(r => r.ToDto()).ToList();
        }

        public async Task<ReminderRunResponseDto> AcknowledgeRunAsync(Guid runId, Guid userId)
        {
            var run = await _repo.GetRunByIdAsync(runId, userId)
                ?? throw new InvalidOperationException("Reminder run not found");

            if (run.Status == ReminderRunStatus.Completed)
                throw new InvalidOperationException("Run already acknowledged");

            run.Status = ReminderRunStatus.Completed;
            run.CompletedAt = DateTime.UtcNow;
            run.UpdatedAt = DateTime.UtcNow;

            await _repo.SaveChangesAsync();
            return run.ToDto();
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

        private static DateTime NextOccurrence(DaysOfWeek day, TimeSpan time, DateTime after)
        {
            var daysUntil = ((int)day - (int)after.DayOfWeek + 7) % 7;
            if (daysUntil == 0 && after.TimeOfDay > time)
                daysUntil = 7;
            return after.Date.AddDays(daysUntil) + time;
        }
    }
}
