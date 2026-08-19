using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.HealthModule.Repository;
using smart_pet_care_api.Modules.PetModule.Repository;
using smart_pet_care_api.Modules.ReminderModule.DTOs.Requests;
using smart_pet_care_api.Modules.ReminderModule.DTOs.Responses;
using smart_pet_care_api.Modules.ReminderModule.Mapper;
using smart_pet_care_api.Modules.ReminderModule.Repository;

namespace smart_pet_care_api.Modules.ReminderModule.Domain
{
    public class ReminderCompletionService : IReminderCompletionService
    {
        /// <summary>Matches the tolerance health records already allow for clock skew.</summary>
        private static readonly TimeSpan FutureTolerance = TimeSpan.FromMinutes(10);

        private readonly IReminderRepository _reminderRepo;
        private readonly IReminderRecalculationService _recalculation;
        private readonly IHealthRecordRepository _healthRepo;
        private readonly IPetRepository _petRepo;

        public ReminderCompletionService(
            IReminderRepository reminderRepo,
            IReminderRecalculationService recalculation,
            IHealthRecordRepository healthRepo,
            IPetRepository petRepo)
        {
            _reminderRepo = reminderRepo;
            _recalculation = recalculation;
            _healthRepo = healthRepo;
            _petRepo = petRepo;
        }

        public async Task<ReminderCompletionResponseDto> CompleteAsync(
            Guid reminderId, Guid userId, CompleteReminderDto dto)
        {
            var reminder = await _reminderRepo.GetByIdAsync(reminderId)
                ?? throw new InvalidOperationException("Reminder not found");

            if (!await _petRepo.ExistsForUserAsync(reminder.PetId, userId))
                throw new InvalidOperationException("Reminder not found");

            // Every type can be ticked off here, weighing and feeding included. Their logs stay
            // optional: a user who wants the portion or the weight recorded posts the log itself
            // with a reminderId, and a user answering a push in one tap gets the fact and nothing
            // more. Demanding the measurement would only buy invented numbers.
            var performedAt = ReminderMapper.NormalizeToUtc(dto.PerformedAt ?? DateTime.UtcNow);
            if (performedAt > DateTime.UtcNow.Add(FutureTolerance))
                throw new ArgumentException("PerformedAt cannot be in the future");

            if (dto.Note is { Length: > 2000 })
                throw new ArgumentException("Note must be 2000 characters or less");

            // Dosage and provider only reach a health record; accepting them for a bath would
            // quietly drop them.
            var filesHealthRecord = ReminderTypePolicy.ToHealthRecordType(reminder.Type) is not null;
            if (!filesHealthRecord && (dto.Dosage is not null || dto.Provider is not null))
                throw new ArgumentException(
                    $"Dosage and provider do not apply to {reminder.Type} reminders.");

            var outcome = await _recalculation.RegisterCompletionAsync(reminderId, performedAt, dto.Note)
                ?? throw new InvalidOperationException("Reminder not found");

            // The already-recorded branch looks the record up by the stored run's date, not by
            // the one that just arrived: a repeat completion only has to fall on the same day to
            // count as the same one, so the two timestamps need not match to the tick.
            var healthRecordId = outcome.AlreadyRecorded
                ? await FindExistingRecordIdAsync(reminder, outcome.Run.PerformedAt ?? performedAt)
                : await FileHealthRecordAsync(outcome.Reminder, performedAt, dto);

            return new ReminderCompletionResponseDto
            {
                Run = outcome.Run.ToDto(),
                Reminder = outcome.Reminder.ToDto(),
                HealthRecordId = healthRecordId,
                AlreadyRecorded = outcome.AlreadyRecorded
            };
        }

        /// <summary>
        /// Medical completions land in HealthRecords; grooming has no record entity by design
        /// and the closed run is its log. NextDueAt is written from the recalculated trigger
        /// rather than accepted from the client, so the record and the reminder cannot drift.
        /// </summary>
        private async Task<Guid?> FileHealthRecordAsync(
            Reminder reminder, DateTime performedAt, CompleteReminderDto dto)
        {
            var recordType = ReminderTypePolicy.ToHealthRecordType(reminder.Type);
            if (recordType is null) return null;

            var record = new HealthRecord
            {
                PetId = reminder.PetId,
                ReminderId = reminder.Id,
                Type = recordType.Value,
                Title = reminder.Title,
                Description = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim(),
                PerformedAt = performedAt,
                NextDueAt = reminder.NextTriggerAt,
                Dosage = dto.Dosage,
                Provider = dto.Provider,
                CreatedAt = DateTime.UtcNow
            };

            await _healthRepo.AddAsync(record);
            await _healthRepo.SaveChangesAsync();

            return record.Id;
        }

        private async Task<Guid?> FindExistingRecordIdAsync(Reminder reminder, DateTime performedAt)
        {
            if (ReminderTypePolicy.ToHealthRecordType(reminder.Type) is null) return null;

            var record = await _healthRepo.GetByReminderAndPerformedAtAsync(reminder.Id, performedAt);
            return record?.Id;
        }
    }
}
