using Microsoft.EntityFrameworkCore;
using smart_pet_care_api.Data;
using smart_pet_care_api.Infrastructure.Classifier.Contracts;
using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.ActivityModule.Domain;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.WellnessModule.Domain;

public sealed class WellnessDataAggregator(AppDbContext dbContext) : IWellnessDataAggregator
{
    public const int EvaluationDays = 30;
    private const int FeedingConsistencyDays = 7;
    private const int PreventiveCareMonths = 12;
    private static readonly ReminderType[] RoutineCareTypes =
    [
        ReminderType.Grooming,
        ReminderType.Bathing,
        ReminderType.Brushing,
        ReminderType.EarCleaning,
        ReminderType.NailTrimming,
        ReminderType.PawCare,
        ReminderType.TeethCleaning
    ];

    public async Task<ClassifierWellnessRequest> AggregateAsync(
        Guid petId,
        Guid userId,
        string? currentSymptoms,
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken = default)
    {
        var pet = await dbContext.Pets.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == petId && item.UserId == userId, cancellationToken)
            ?? throw new InvalidOperationException("Pet not found");

        var endDate = DateOnly.FromDateTime(evaluatedAt.UtcDateTime);
        var startDate = endDate.AddDays(-(EvaluationDays - 1));
        var windowStart = DateTime.SpecifyKind(startDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var windowEndExclusive = DateTime.SpecifyKind(endDate.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);

        var activityLogs = await dbContext.ActivityLogs.AsNoTracking()
            .Where(item => item.PetId == petId
                && item.RecordedAt >= windowStart
                && item.RecordedAt < windowEndExclusive)
            .ToListAsync(cancellationToken);

        var sleepLogs = await dbContext.SleepLogs.AsNoTracking()
            .Where(item => item.PetId == petId
                && item.SleepDate >= windowStart
                && item.SleepDate < windowEndExclusive)
            .ToListAsync(cancellationToken);

        var feedings = await dbContext.FeedingLogs.AsNoTracking()
            .Where(item => item.PetId == petId
                && item.FedAt >= windowStart
                && item.FedAt < windowEndExclusive)
            .ToListAsync(cancellationToken);

        var conditions = await dbContext.PetConditions.AsNoTracking()
            .Where(item => item.PetId == petId && item.IsActive)
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);

        var medications = await dbContext.PetMedications.AsNoTracking()
            .Where(item => item.PetId == petId
                && item.StartDate < windowEndExclusive
                && (item.EndDate == null || item.EndDate >= evaluatedAt.UtcDateTime))
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);

        var medicationRuns = await (
            from reminder in dbContext.Reminders.AsNoTracking()
            join run in dbContext.ReminderRuns.AsNoTracking() on reminder.Id equals run.ReminderId
            where reminder.PetId == petId
                && reminder.SourceId != null
                && run.ScheduledFor >= windowStart
                && run.ScheduledFor < windowEndExclusive
                && (run.Type == ReminderType.Medication || reminder.Type == ReminderType.Medication)
            select new MedicationRun(reminder.SourceId!.Value, run.Status))
            .ToListAsync(cancellationToken);

        var weights = await dbContext.PetWeightLogs.AsNoTracking()
            .Where(item => item.PetId == petId
                && item.MeasuredAt >= windowStart
                && item.MeasuredAt < windowEndExclusive)
            .OrderBy(item => item.MeasuredAt)
            .ToListAsync(cancellationToken);

        var preventiveSince = evaluatedAt.UtcDateTime.AddMonths(-PreventiveCareMonths);
        var preventiveEvents = await dbContext.PetEvents.AsNoTracking()
            .Where(item => item.PetId == petId
                && item.ScheduledAt >= preventiveSince
                && item.ScheduledAt <= evaluatedAt.UtcDateTime
                && (item.Type == PetEventType.VetVisit
                    || item.Type == PetEventType.Checkup
                    || item.Type == PetEventType.Vaccination))
            .ToListAsync(cancellationToken);

        var routineCareReminders = await dbContext.Reminders.AsNoTracking()
            .Where(item => item.PetId == petId && RoutineCareTypes.Contains(item.Type))
            .Select(item => new RoutineCareRecord(item.Type, item.LastCompletedAt))
            .ToListAsync(cancellationToken);

        var completedGroomingEvents = await dbContext.PetEvents.AsNoTracking()
            .Where(item => item.PetId == petId
                && item.Type == PetEventType.Grooming
                && item.Status == PetEventStatus.Completed
                && item.ScheduledAt <= evaluatedAt.UtcDateTime)
            .Select(item => item.ScheduledAt)
            .ToListAsync(cancellationToken);

        var previousScore = await dbContext.PetWellnessAssessments.AsNoTracking()
            .Where(item => item.PetId == petId
                && item.EvaluatedAt < evaluatedAt.UtcDateTime
                && item.WellnessScore != null)
            .OrderByDescending(item => item.EvaluatedAt)
            .Select(item => item.WellnessScore)
            .FirstOrDefaultAsync(cancellationToken);

        return new ClassifierWellnessRequest
        {
            Pet = MapPet(pet, evaluatedAt),
            Activity = MapActivity(activityLogs, sleepLogs),
            Feeding = MapFeeding(feedings, endDate),
            ActiveConditions = conditions.Select(item => new ClassifierWellnessCondition
            {
                Name = item.Name,
                TypeLabel = item.Type.ToString()
            }).ToList(),
            ActiveMedications = medications.Select(item => MapMedication(item, medicationRuns)).ToList(),
            WeightHistory = weights.Select(item => new ClassifierWellnessWeightMeasurement
            {
                WeightKg = item.WeightKg,
                MeasuredAt = AsUtcOffset(item.MeasuredAt)
            }).ToList(),
            PreventiveCare = MapPreventiveCare(preventiveEvents),
            RoutineCare = MapRoutineCare(routineCareReminders, completedGroomingEvents),
            CurrentSymptoms = NormalizeOptional(currentSymptoms),
            PreviousScore = previousScore,
            EvaluationWindow = new ClassifierWellnessEvaluationWindow
            {
                StartDate = startDate,
                EndDate = endDate
            }
        };
    }

    private static ClassifierWellnessPet MapPet(Pet pet, DateTimeOffset evaluatedAt) => new()
    {
        Species = SpeciesWireValue(pet.Species),
        Breed = NormalizeOptional(pet.Breed),
        AgeMonths = pet.BirthDate is null ? null : FullMonths(pet.BirthDate.Value, evaluatedAt.UtcDateTime),
        Sex = pet.Sex == Sex.Unknown ? null : pet.Sex.ToString().ToLowerInvariant(),
        WeightKg = pet.WeightKg,
        BehavioralNotes = pet.BehavioralNotes is { Count: > 0 }
            ? string.Join("; ", pet.BehavioralNotes.Where(note => !string.IsNullOrWhiteSpace(note)))
            : null
    };

    private static ClassifierWellnessActivity? MapActivity(
        IReadOnlyList<ActivityLog> activityLogs,
        IReadOnlyList<SleepLog> sleepLogs)
    {
        var activityByDay = activityLogs
            .Select(item => new
            {
                Date = item.RecordedAt.Date,
                item.Steps,
                ActiveMinutes = ActivityEffort.ActiveMinutes(item.DurationMinutes, item.Intensity)
            })
            .Where(item => item.Steps.HasValue || item.ActiveMinutes.HasValue)
            .GroupBy(item => item.Date)
            .Select(group => new
            {
                Date = group.Key,
                Steps = SumNullable(group.Select(item => item.Steps)),
                ActiveMinutes = SumNullable(group.Select(item => item.ActiveMinutes))
            })
            .ToList();

        var sleepByDay = sleepLogs
            .GroupBy(item => item.SleepDate.Date)
            .Select(group => new
            {
                Date = group.Key,
                Hours = group.Sum(item => item.Hours)
            })
            .ToList();

        if (activityByDay.Count == 0 && sleepByDay.Count == 0) return null;

        return new ClassifierWellnessActivity
        {
            AvgStepsPerDay = Average(activityByDay
                .Where(item => item.Steps.HasValue)
                .Select(item => (decimal)item.Steps!.Value)),
            AvgActiveMinutesPerDay = Average(activityByDay
                .Where(item => item.ActiveMinutes.HasValue)
                .Select(item => (decimal)item.ActiveMinutes!.Value)),
            AvgSleepHoursPerDay = Average(sleepByDay.Select(item => item.Hours)),
            DaysTracked = activityByDay.Select(item => item.Date)
                .Concat(sleepByDay.Select(item => item.Date))
                .Distinct()
                .Count()
        };
    }

    private static int? SumNullable(IEnumerable<int?> values)
    {
        var materialized = values
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToList();
        return materialized.Count == 0 ? null : materialized.Sum();
    }

    private static ClassifierWellnessFeeding? MapFeeding(IReadOnlyList<FeedingLog> rows, DateOnly endDate)
    {
        if (rows.Count == 0) return null;
        var calorieRows = rows.Where(item => item.ApproxCalories.HasValue).ToList();
        var consistencyStart = endDate.AddDays(-(FeedingConsistencyDays - 1));
        return new ClassifierWellnessFeeding
        {
            AvgMealsPerDay = decimal.Round(rows.Count / (decimal)EvaluationDays, 2),
            AvgCaloriesPerDay = calorieRows.Count == 0
                ? null
                : decimal.Round(calorieRows.Sum(item => item.ApproxCalories!.Value) / (decimal)EvaluationDays, 2),
            FoodTypes = rows.Select(item => item.FoodType.ToString()).Distinct().Order().ToList(),
            ConsistencyDays = rows.Select(item => DateOnly.FromDateTime(item.FedAt))
                .Where(date => date >= consistencyStart && date <= endDate)
                .Distinct().Count()
        };
    }

    private static ClassifierWellnessMedication MapMedication(
        PetMedication medication,
        IReadOnlyList<MedicationRun> runs)
    {
        var relevant = runs.Where(item => item.MedicationId == medication.Id).ToList();
        return new ClassifierWellnessMedication
        {
            Name = medication.Name,
            Frequency = medication.MedicationFrequency.ToString(),
            ScheduledDoses = relevant.Count,
            CompletedDoses = relevant.Count(item => item.Status == ReminderRunStatus.Completed)
        };
    }

    private static ClassifierWellnessPreventiveCare? MapPreventiveCare(IReadOnlyList<PetEvent> events)
    {
        if (events.Count == 0) return null;
        return new ClassifierWellnessPreventiveCare
        {
            RecentVetVisit = events.Any(item => item.Status == PetEventStatus.Completed
                && item.Type is PetEventType.VetVisit or PetEventType.Checkup),
            VaccinationsUpToDate = events.Any(item => item.Status == PetEventStatus.Completed
                && item.Type == PetEventType.Vaccination)
        };
    }

    private static IReadOnlyList<ClassifierWellnessRoutineCareEntry> MapRoutineCare(
        IReadOnlyList<RoutineCareRecord> reminders,
        IReadOnlyList<DateTime> completedGroomingEvents)
    {
        var entries = reminders
            .GroupBy(item => item.Type)
            .Select(group => new ClassifierWellnessRoutineCareEntry
            {
                Type = MapReminderType(group.Key),
                LastDoneAt = AsDateOnly(group.Max(item => item.LastCompletedAt))
            })
            .ToDictionary(item => item.Type);

        var latestGroomingEvent = completedGroomingEvents
            .Select(item => (DateTime?)item)
            .Max();
        if (latestGroomingEvent is not null)
        {
            var eventDate = AsDateOnly(latestGroomingEvent);
            if (!entries.TryGetValue(ClassifierWellnessReminderType.Grooming, out var grooming)
                || grooming.LastDoneAt is null
                || eventDate > grooming.LastDoneAt)
            {
                entries[ClassifierWellnessReminderType.Grooming] = new ClassifierWellnessRoutineCareEntry
                {
                    Type = ClassifierWellnessReminderType.Grooming,
                    LastDoneAt = eventDate
                };
            }
        }

        return entries.Values.OrderBy(item => item.Type).ToList();
    }

    private static ClassifierWellnessReminderType MapReminderType(ReminderType type) =>
        Enum.Parse<ClassifierWellnessReminderType>(type.ToString());

    private static DateOnly? AsDateOnly(DateTime? value) => value is null
        ? null
        : DateOnly.FromDateTime(value.Value);

    private static decimal? Average(IEnumerable<decimal> values)
    {
        var materialized = values.ToList();
        return materialized.Count == 0 ? null : decimal.Round(materialized.Average(), 2);
    }

    private static int FullMonths(DateTime birthDate, DateTime evaluatedAt)
    {
        var months = (evaluatedAt.Year - birthDate.Year) * 12 + evaluatedAt.Month - birthDate.Month;
        if (evaluatedAt.Day < birthDate.Day) months--;
        return Math.Clamp(months, 0, 600);
    }

    private static string SpeciesWireValue(AnimalSpecies species) => species switch
    {
        AnimalSpecies.GuineaPig => "guinea_pig",
        AnimalSpecies.Unknown => "other",
        _ => species.ToString().ToLowerInvariant()
    };

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DateTimeOffset AsUtcOffset(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private sealed record MedicationRun(Guid MedicationId, ReminderRunStatus Status);
    private sealed record RoutineCareRecord(ReminderType Type, DateTime? LastCompletedAt);
}
