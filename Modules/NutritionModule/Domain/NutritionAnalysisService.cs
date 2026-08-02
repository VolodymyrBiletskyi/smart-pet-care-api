using System.Globalization;
using System.Text;
using smart_pet_care_api.Infrastructure.Classifier;
using smart_pet_care_api.Infrastructure.Classifier.Contracts;
using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.ChatModule.Domain;
using smart_pet_care_api.Modules.NutritionModule.DTOs.Responses;
using smart_pet_care_api.Modules.NutritionModule.Mapper;
using smart_pet_care_api.Modules.NutritionModule.Repository;
using smart_pet_care_api.Modules.PetModule.Repository;

namespace smart_pet_care_api.Modules.NutritionModule.Domain
{
    public class NutritionAnalysisService : INutritionAnalysisService
    {
        /// <summary>Latest plus the one before it.</summary>
        public const int RetainedAnalysesPerPet = 2;

        private const int AverageDaysPerMonth = 30;

        private readonly INutritionService _nutritionService;
        private readonly INutritionAnalysisRepository _analysisRepo;
        private readonly IPetRepository _petRepo;
        private readonly IClassifierClient _classifier;

        public NutritionAnalysisService(
            INutritionService nutritionService,
            INutritionAnalysisRepository analysisRepo,
            IPetRepository petRepo,
            IClassifierClient classifier)
        {
            _nutritionService = nutritionService;
            _analysisRepo = analysisRepo;
            _petRepo = petRepo;
            _classifier = classifier;
        }

        public async Task<NutritionAnalysisResponseDto> AnalyzeAsync(
            Guid petId,
            Guid userId,
            DateOnly? date,
            int utcOffsetMinutes,
            CancellationToken cancellationToken = default)
        {
            var pet = await _petRepo.GetByIdAndUserIdAsync(petId, userId)
                ?? throw new InvalidOperationException("Pet not found");

            // Ownership and offset validation live in the summary service; this
            // keeps the analysed figures identical to what GET returns.
            var summary = await _nutritionService.GetDailySummaryAsync(
                petId, userId, date, utcOffsetMinutes);

            // The classifier has no single nutrition route yet, so the graded
            // figures come from `wellness` and the prose from `chat`. Both are
            // issued together because neither depends on the other's result.
            var wellnessTask = _classifier.AnalyzeWellnessAsync(
                BuildWellnessRequest(pet, summary),
                cancellationToken);
            var chatTask = _classifier.ChatAsync(
                BuildChatRequest(pet, summary),
                cancellationToken);

            await Task.WhenAll(wellnessTask, chatTask);

            var analysis = NutritionAnalysisMapper.ToEntity(
                wellnessTask.Result, chatTask.Result, summary);
            await _analysisRepo.AddAsync(analysis);
            await _analysisRepo.SaveChangesAsync();

            await TrimToRetainedAsync(petId);

            return analysis.ToDto();
        }

        public async Task<NutritionAnalysisHistoryResponseDto> GetRecentAsync(Guid petId, Guid userId)
        {
            if (!await _petRepo.ExistsForUserAsync(petId, userId))
                throw new InvalidOperationException("Pet not found");

            var analyses = await _analysisRepo.GetRecentByPetIdAsync(petId, RetainedAnalysesPerPet);

            return new NutritionAnalysisHistoryResponseDto
            {
                Latest = analyses.Count > 0 ? analyses[0].ToDto() : null,
                Previous = analyses.Count > 1 ? analyses[1].ToDto() : null
            };
        }

        /// <summary>
        /// Asks the wellness route to score the day's feeding. Only the pet and
        /// feeding dimensions are supplied, so the overall wellness score comes
        /// back null — the analysis reads the diet dimension alone.
        /// </summary>
        private static ClassifierWellnessRequest BuildWellnessRequest(
            Pet pet, DailyNutritionSummaryResponseDto summary)
        {
            var date = summary.Date.ToString("yyyy-MM-dd");

            return new ClassifierWellnessRequest
            {
                Pet = new ClassifierWellnessPet
                {
                    Species = PetTypeMapper.ToClassifierPetType(PetTypeMapper.Map(pet.Species)),
                    Breed = pet.Breed,
                    AgeMonths = ToAgeMonths(pet.BirthDate),
                    WeightKg = pet.WeightKg
                },
                Feeding = new ClassifierWellnessFeeding
                {
                    AvgMealsPerDay = summary.MealCount,
                    AvgCaloriesPerDay = summary.TotalCalories,

                    // One analysed day is one day of history. The diet dimension
                    // scores tracking consistency too, which is why the grade is
                    // taken from the calorie ratio rather than that score.
                    ConsistencyDays = 1
                },
                EvaluationWindow = new ClassifierWellnessEvaluationWindow
                {
                    StartDate = date,
                    EndDate = date
                }
            };
        }

        /// <summary>
        /// Puts the day's figures to the chat route as a single user turn and
        /// asks for a bulleted reply, which the mapper splits into the summary
        /// and the advice list. No session is created — this is a one-shot turn.
        /// </summary>
        private static ClassifierChatRequest BuildChatRequest(
            Pet pet, DailyNutritionSummaryResponseDto summary)
        {
            return new ClassifierChatRequest
            {
                PetType = PetTypeMapper.ToClassifierPetType(PetTypeMapper.Map(pet.Species)),
                Messages =
                [
                    new ClassifierChatMessage
                    {
                        Role = ClassifierChatRole.User,
                        Content = BuildChatPrompt(pet, summary)
                    }
                ]
            };
        }

        private static string BuildChatPrompt(Pet pet, DailyNutritionSummaryResponseDto summary)
        {
            var prompt = new StringBuilder();
            prompt.Append("Review my ").Append(pet.Species);

            if (!string.IsNullOrWhiteSpace(pet.Breed))
                prompt.Append(" (").Append(pet.Breed).Append(')');

            var ageMonths = ToAgeMonths(pet.BirthDate);
            if (pet.WeightKg is { } weight)
                prompt.Append(", ").Append(weight.ToString("0.#", CultureInfo.InvariantCulture)).Append(" kg");
            if (ageMonths is { } age)
                prompt.Append(", ").Append(age).Append(" months old");

            prompt.Append("'s feeding for ").Append(summary.Date.ToString("yyyy-MM-dd")).AppendLine(".");
            prompt.Append("Meals logged: ").Append(summary.MealCount).AppendLine();
            prompt.Append("Total calories: ").Append(summary.TotalCalories).AppendLine();

            foreach (var portion in summary.PortionTotalsByUnit)
            {
                prompt.Append("Portions: ")
                    .Append(portion.TotalAmount.ToString("0.#", CultureInfo.InvariantCulture))
                    .Append(' ').Append(portion.Unit).AppendLine();
            }

            prompt.Append("Scheduled feedings: ").Append(summary.ScheduledFeedings).AppendLine();

            if (summary.Goal is { } goal)
            {
                prompt.Append("Daily goal:");
                if (goal.DailyCalorieTarget is { } calories)
                    prompt.Append(' ').Append(calories).Append(" kcal;");
                if (goal.DailyPortionTarget is { } portion)
                    prompt.Append(' ').Append(portion.ToString("0.#", CultureInfo.InvariantCulture))
                        .Append(' ').Append(goal.PortionUnit).Append(';');
                if (goal.MealsPerDay is { } meals)
                    prompt.Append(' ').Append(meals).Append(" meals;");
                prompt.AppendLine();
            }

            if (summary.Comparison is { } comparison)
            {
                if (comparison.CaloriesRemaining is { } caloriesLeft)
                    prompt.Append("Calories remaining against the goal: ").Append(caloriesLeft).AppendLine();
                if (comparison.MealsRemaining is { } mealsLeft)
                    prompt.Append("Meals remaining against the goal: ").Append(mealsLeft).AppendLine();
                if (comparison.PortionRemaining is { } portionLeft)
                    prompt.Append("Portion remaining against the goal: ")
                        .Append(portionLeft.ToString("0.#", CultureInfo.InvariantCulture)).AppendLine();
            }

            prompt.AppendLine()
                .AppendLine("Summarise the day in one or two sentences, then give up to three short, "
                    + "actionable feeding tips. Put each tip on its own line beginning with a dash.");

            return prompt.ToString();
        }

        private static ClassifierNutritionRequest BuildRequest(
            Pet pet, DailyNutritionSummaryResponseDto summary)
        {
            var goal = summary.Goal;

            return new ClassifierNutritionRequest
            {
                RequestId = Guid.NewGuid().ToString("D"),
                PetType = PetTypeMapper.ToClassifierPetType(PetTypeMapper.Map(pet.Species)),
                Breed = pet.Breed,
                WeightKg = pet.WeightKg,
                AgeMonths = ToAgeMonths(pet.BirthDate),
                Date = summary.Date.ToString("yyyy-MM-dd"),
                MealCount = summary.MealCount,
                TotalCalories = summary.TotalCalories,
                PortionTotals = summary.PortionTotalsByUnit
                    .Select(total => new ClassifierNutritionPortionTotal
                    {
                        Unit = NutritionAnalysisMapper.ToClassifierPortionUnit(total.Unit),
                        TotalAmount = total.TotalAmount
                    })
                    .ToList(),
                ScheduledFeedings = summary.ScheduledFeedings,
                Goal = goal is null ? null : new ClassifierNutritionGoal
                {
                    DailyCalorieTarget = goal.DailyCalorieTarget,
                    DailyPortionTarget = goal.DailyPortionTarget,
                    PortionUnit = goal.PortionUnit is null
                        ? null
                        : NutritionAnalysisMapper.ToClassifierPortionUnit(goal.PortionUnit.Value),
                    MealsPerDay = goal.MealsPerDay
                },
                Comparison = summary.Comparison is null ? null : new ClassifierNutritionComparison
                {
                    CaloriesRemaining = summary.Comparison.CaloriesRemaining,
                    CalorieTargetMet = summary.Comparison.CalorieTargetMet,
                    MealsRemaining = summary.Comparison.MealsRemaining,
                    MealsTargetMet = summary.Comparison.MealsTargetMet,
                    PortionRemaining = summary.Comparison.PortionRemaining,
                    PortionTargetMet = summary.Comparison.PortionTargetMet
                }
            };
        }

        private static int? ToAgeMonths(DateTime? birthDate)
        {
            if (birthDate is null)
                return null;

            var days = (DateTime.UtcNow - birthDate.Value).TotalDays;
            return days < 0 ? null : (int)(days / AverageDaysPerMonth);
        }

        private async Task TrimToRetainedAsync(Guid petId)
        {
            var stored = await _analysisRepo.GetTrackedByPetIdAsync(petId);
            if (stored.Count <= RetainedAnalysesPerPet)
                return;

            _analysisRepo.DeleteRange(stored.Skip(RetainedAnalysesPerPet));
            await _analysisRepo.SaveChangesAsync();
        }
    }
}
