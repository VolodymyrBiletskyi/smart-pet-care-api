using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.FeedingModule.Repository;
using smart_pet_care_api.Modules.NutritionModule.DTOs.Requests;
using smart_pet_care_api.Modules.NutritionModule.DTOs.Responses;
using smart_pet_care_api.Modules.NutritionModule.Mapper;
using smart_pet_care_api.Modules.NutritionModule.Repository;
using smart_pet_care_api.Modules.PetModule.Repository;
using smart_pet_care_api.Modules.ReminderModule.Repository;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.NutritionModule.Domain
{
    public class NutritionService : INutritionService
    {
        // ±14h covers every real-world UTC offset (matches the widest civil offsets).
        private const int MaxOffsetMinutes = 14 * 60;

        private readonly INutritionGoalRepository _goalRepo;
        private readonly IPetRepository _petRepo;
        private readonly IFeedingLogRepository _feedingRepo;
        private readonly IReminderRepository _reminderRepo;

        public NutritionService(
            INutritionGoalRepository goalRepo,
            IPetRepository petRepo,
            IFeedingLogRepository feedingRepo,
            IReminderRepository reminderRepo)
        {
            _goalRepo = goalRepo;
            _petRepo = petRepo;
            _feedingRepo = feedingRepo;
            _reminderRepo = reminderRepo;
        }

        public async Task<NutritionGoalResponseDto?> GetGoalAsync(Guid petId, Guid userId)
        {
            await EnsurePetBelongsToUserAsync(petId, userId);

            var goal = await _goalRepo.GetByPetIdAsync(petId);
            return goal?.ToDto();
        }

        public async Task<NutritionGoalResponseDto> UpsertGoalAsync(Guid petId, Guid userId, UpsertNutritionGoalDto dto)
        {
            await EnsurePetBelongsToUserAsync(petId, userId);

            var goal = await _goalRepo.GetTrackedByPetIdAsync(petId);
            if (goal is null)
            {
                goal = NutritionGoalMapper.ToEntity(dto, petId);
                ValidateFinalState(goal);
                await _goalRepo.AddAsync(goal);
            }
            else
            {
                goal.ApplyUpsert(dto);
                ValidateFinalState(goal);
            }

            await _goalRepo.SaveChangesAsync();
            return goal.ToDto();
        }

        public async Task<NutritionGoalResponseDto> PatchGoalAsync(Guid petId, Guid userId, PatchNutritionGoalDto dto)
        {
            await EnsurePetBelongsToUserAsync(petId, userId);

            var goal = await _goalRepo.GetTrackedByPetIdAsync(petId)
                ?? throw new InvalidOperationException("Nutrition goal not found");

            goal.PatchEntity(dto);
            ValidateFinalState(goal);

            await _goalRepo.SaveChangesAsync();
            return goal.ToDto();
        }

        public async Task<bool> DeleteGoalAsync(Guid petId, Guid userId)
        {
            await EnsurePetBelongsToUserAsync(petId, userId);

            var goal = await _goalRepo.GetTrackedByPetIdAsync(petId);
            if (goal is null) return false;

            _goalRepo.Delete(goal);
            await _goalRepo.SaveChangesAsync();
            return true;
        }

        public async Task<DailyNutritionSummaryResponseDto> GetDailySummaryAsync(
            Guid petId, Guid userId, DateOnly? date, int utcOffsetMinutes)
        {
            await EnsurePetBelongsToUserAsync(petId, userId);

            if (utcOffsetMinutes < -MaxOffsetMinutes || utcOffsetMinutes > MaxOffsetMinutes)
                throw new ArgumentException("utcOffsetMinutes is out of range");

            // Default to "today" in the caller's local time.
            var localDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow.AddMinutes(utcOffsetMinutes));

            // Local midnight → UTC. Npgsql requires Kind=Utc for timestamptz comparisons.
            var startUtc = DateTime.SpecifyKind(
                localDate.ToDateTime(TimeOnly.MinValue).AddMinutes(-utcOffsetMinutes), DateTimeKind.Utc);
            var endUtc = startUtc.AddDays(1);

            var logs = await _feedingRepo.GetByPetIdAndRangeAsync(petId, startUtc, endUtc);
            var goal = await _goalRepo.GetByPetIdAsync(petId);
            var reminders = await _reminderRepo.GetByPetIdAsync(petId);

            var portionTotals = logs
                .Where(l => l.PortionAmount.HasValue && l.PortionUnit.HasValue)
                .GroupBy(l => l.PortionUnit!.Value)
                .Select(g => new PortionTotalDto { Unit = g.Key, TotalAmount = g.Sum(x => x.PortionAmount!.Value) })
                .OrderBy(p => p.Unit)
                .ToList();

            var summary = new DailyNutritionSummaryResponseDto
            {
                PetId = petId,
                Date = localDate,
                UtcOffsetMinutes = utcOffsetMinutes,
                MealCount = logs.Count,
                TotalCalories = logs.Sum(l => l.ApproxCalories ?? 0),
                PortionTotalsByUnit = portionTotals,
                ScheduledFeedings = reminders.Count(r =>
                    r.Type == ReminderType.Feeding && r.Status == ReminderStatus.Active),
                Goal = goal?.ToDto()
            };

            if (goal is not null)
                summary.Comparison = BuildComparison(goal, summary.TotalCalories, summary.MealCount, portionTotals);

            return summary;
        }

        private static NutritionComparisonDto BuildComparison(
            NutritionGoal goal, int totalCalories, int mealCount, List<PortionTotalDto> portionTotals)
        {
            var comparison = new NutritionComparisonDto();

            if (goal.DailyCalorieTarget.HasValue)
            {
                comparison.CaloriesRemaining = goal.DailyCalorieTarget.Value - totalCalories;
                comparison.CalorieTargetMet = totalCalories >= goal.DailyCalorieTarget.Value;
            }

            if (goal.MealsPerDay.HasValue)
            {
                comparison.MealsRemaining = goal.MealsPerDay.Value - mealCount;
                comparison.MealsTargetMet = mealCount >= goal.MealsPerDay.Value;
            }

            // Portion comparison is meaningful only against the goal's own unit; totals in
            // other units are left out to avoid mixing grams/ml/cups.
            if (goal.DailyPortionTarget.HasValue && goal.PortionUnit.HasValue)
            {
                var loggedInUnit = portionTotals
                    .Where(p => p.Unit == goal.PortionUnit.Value)
                    .Sum(p => p.TotalAmount);

                comparison.PortionRemaining = goal.DailyPortionTarget.Value - loggedInUnit;
                comparison.PortionTargetMet = loggedInUnit >= goal.DailyPortionTarget.Value;
            }

            return comparison;
        }

        private async Task EnsurePetBelongsToUserAsync(Guid petId, Guid userId)
        {
            if (!await _petRepo.ExistsForUserAsync(petId, userId))
                throw new InvalidOperationException("Pet not found");
        }

        private static void ValidateFinalState(NutritionGoal goal)
        {
            if (goal.DailyCalorieTarget is < 0)
                throw new ArgumentException("DailyCalorieTarget cannot be negative");
            if (goal.DailyPortionTarget is < 0)
                throw new ArgumentException("DailyPortionTarget cannot be negative");
            if (goal.MealsPerDay is < 0)
                throw new ArgumentException("MealsPerDay cannot be negative");

            if (goal.PortionUnit.HasValue && !Enum.IsDefined(goal.PortionUnit.Value))
                throw new ArgumentException("PortionUnit is invalid");

            if (goal.DailyPortionTarget.HasValue && !goal.PortionUnit.HasValue)
                throw new ArgumentException("PortionUnit is required when DailyPortionTarget is specified");
        }
    }
}
