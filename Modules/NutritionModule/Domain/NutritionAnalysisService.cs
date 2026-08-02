using smart_pet_care_api.Infrastructure.Classifier;
using smart_pet_care_api.Infrastructure.Classifier.Contracts;
using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.ChatModule.Domain;
using smart_pet_care_api.Modules.FeedingModule.Repository;
using smart_pet_care_api.Modules.NutritionModule.DTOs.Requests;
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

        // Limits the classifier enforces on `feeding-summary`. Breaking one is
        // a 422, which surfaces to the client as a 502, so the request is
        // shaped to fit rather than sent optimistically.
        private const decimal MaxWeightKg = 500m;
        private const int MaxAgeMonths = 600;
        private const int MaxProducts = 100;
        private const int MaxProductNameLength = 200;
        private const decimal MaxProductCalories = 20000m;

        // Status bands used when a goal replaces the classifier's target. They
        // reproduce every classifier result observed on the live route: -35.1%
        // is UNDER_TARGET, -100% is EXTREME_UNDER_TARGET, +981.2% is
        // EXTREME_OVER_TARGET.
        private const decimal OnTargetDeviationPct = 10m;
        private const decimal ExtremeDeviationPct = 50m;

        private readonly INutritionAnalysisRepository _analysisRepo;
        private readonly IPetRepository _petRepo;
        private readonly IFeedingLogRepository _feedingRepo;
        private readonly INutritionGoalRepository _goalRepo;
        private readonly IClassifierClient _classifier;

        public NutritionAnalysisService(
            INutritionAnalysisRepository analysisRepo,
            IPetRepository petRepo,
            IFeedingLogRepository feedingRepo,
            INutritionGoalRepository goalRepo,
            IClassifierClient classifier)
        {
            _analysisRepo = analysisRepo;
            _petRepo = petRepo;
            _feedingRepo = feedingRepo;
            _goalRepo = goalRepo;
            _classifier = classifier;
        }

        public async Task<NutritionAnalysisResponseDto> AnalyzeAsync(
            Guid petId,
            Guid userId,
            DateOnly? date,
            int utcOffsetMinutes,
            NutritionAnalysisRequestDto? request = null,
            CancellationToken cancellationToken = default)
        {
            var pet = await _petRepo.GetByIdAndUserIdAsync(petId, userId)
                ?? throw new InvalidOperationException("Pet not found");

            // Rejected before the day's logs are read — neither the weight nor
            // the offset check depends on them.
            var weightKg = RequireWeight(request?.WeightKg ?? pet.WeightKg);

            // The same window the daily summary reads, so an analysis always
            // grades the day the client is looking at. It is resolved even when
            // the caller supplies products, because the stored analysis is
            // still filed under a local date.
            var (localDate, startUtc, endUtc) = NutritionService.ResolveDayWindow(date, utcOffsetMinutes);

            // Supplied products replace the day's logs outright; the logs are
            // not even read, so a caller can grade a meal plan that was never
            // recorded. An empty array is a deliberate "ate nothing".
            var (products, mealCount) = request?.Products is { } supplied
                ? (BuildProducts(supplied), supplied.Count)
                : await ReadLoggedProductsAsync(petId, startUtc, endUtc);

            var response = await _classifier.SummarizeFeedingAsync(
                BuildRequest(pet, weightKg, products, request),
                cancellationToken);

            var result = FindResult(response, petId);

            // A user-set calorie goal outranks the target the classifier
            // derived from body weight. Read after the call so a classifier
            // failure costs no query.
            var goal = await _goalRepo.GetByPetIdAsync(petId);
            result = ApplyGoalTarget(result, goal);

            var analysis = NutritionAnalysisMapper.ToEntity(
                result, response.Disclaimer, petId, localDate, utcOffsetMinutes, mealCount);
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
        /// Asks the classifier to grade one pet. The route takes a batch, so the
        /// pet list holds a single entry and the matching result is picked out
        /// of the response by <c>petId</c>.
        /// </summary>
        private static ClassifierFeedingSummaryRequest BuildRequest(
            Pet pet,
            decimal weightKg,
            IReadOnlyList<ClassifierFeedingProduct> products,
            NutritionAnalysisRequestDto? request)
        {
            var species = request?.Species ?? pet.Species;

            return new ClassifierFeedingSummaryRequest
            {
                Pets =
                [
                    new ClassifierFeedingSummaryPet
                    {
                        PetId = pet.Id.ToString("D"),
                        Species = PetTypeMapper.ToClassifierPetType(PetTypeMapper.Map(species)),
                        Breed = request?.Breed ?? pet.Breed,
                        WeightKg = weightKg,
                        AgeMonths = request?.AgeMonths ?? ToAgeMonths(pet.BirthDate),
                        Products = products
                    }
                ]
            };
        }

        /// <summary>
        /// The classifier derives the calorie target from body weight, so it has
        /// no way to grade a pet that has none — whether the caller supplied one
        /// or the pet has one recorded.
        /// </summary>
        private static decimal RequireWeight(decimal? weightKg)
        {
            if (weightKg is not { } weight || weight <= 0 || weight > MaxWeightKg)
                throw new ArgumentException(
                    $"A weight above 0 and at most {MaxWeightKg:0} kg is needed before feeding "
                    + "can be analysed — send weightKg or record one on the pet");

            return weight;
        }

        private async Task<(List<ClassifierFeedingProduct> Products, int MealCount)>
            ReadLoggedProductsAsync(Guid petId, DateTime startUtc, DateTime endUtc)
        {
            var logs = await _feedingRepo.GetByPetIdAndRangeAsync(petId, startUtc, endUtc);
            return (BuildProducts(logs), logs.Count);
        }

        /// <summary>
        /// Maps caller-supplied products onto the wire contract. Model
        /// validation has already enforced the classifier's limits; the clamp
        /// and truncation here keep a service-level caller from bypassing them.
        /// Entries are passed through in the order given rather than merged,
        /// because the caller chose that list.
        /// </summary>
        private static List<ClassifierFeedingProduct> BuildProducts(
            IReadOnlyList<NutritionAnalysisProductDto> products)
        {
            return products
                .Take(MaxProducts)
                .Select(product => new ClassifierFeedingProduct
                {
                    Name = TruncateName(product.Name.Trim()),
                    Calories = ToProductCalories(product.Calories)
                })
                .ToList();
        }

        /// <summary>
        /// Collapses the day's feeding logs into the product list the classifier
        /// grades. Logs of the same food are merged so the list stays inside the
        /// route's limits without losing calories.
        /// </summary>
        private static List<ClassifierFeedingProduct> BuildProducts(IReadOnlyList<FeedingLog> logs)
        {
            var products = logs
                .GroupBy(ProductName)
                .Select(group => new ClassifierFeedingProduct
                {
                    Name = group.Key,
                    Calories = ToProductCalories(group.Sum(log => (decimal)(log.ApproxCalories ?? 0)))
                })
                .OrderByDescending(product => product.Calories)
                .ThenBy(product => product.Name, StringComparer.Ordinal)
                .ToList();

            if (products.Count <= MaxProducts)
                return products;

            // Over the route's cap the smallest entries are merged rather than
            // dropped, because the calorie total is what gets graded.
            var kept = products.Take(MaxProducts - 1).ToList();
            kept.Add(new ClassifierFeedingProduct
            {
                Name = "Other foods",
                Calories = ToProductCalories(
                    products.Skip(MaxProducts - 1).Sum(product => product.Calories))
            });

            return kept;
        }

        /// <summary>The food's own name, falling back to its type when unnamed.</summary>
        private static string ProductName(FeedingLog log)
        {
            var name = log.FoodName?.Trim();
            if (string.IsNullOrEmpty(name))
                name = log.FoodType.ToString();

            return TruncateName(name);
        }

        private static string TruncateName(string name) =>
            name.Length <= MaxProductNameLength ? name : name[..MaxProductNameLength];

        private static decimal ToProductCalories(decimal calories) =>
            Math.Clamp(calories, 0m, MaxProductCalories);

        /// <summary>
        /// Re-grades the classifier's verdict against the pet's own calorie
        /// goal. The <c>feeding-summary</c> route accepts no target input — it
        /// always derives one from body weight — so a user-set goal can only be
        /// applied here, after the response. Deviation and status are
        /// recomputed alongside the target, because a stored row whose
        /// deviation was measured against a different target contradicts
        /// itself.
        /// </summary>
        private static ClassifierFeedingSummaryResult ApplyGoalTarget(
            ClassifierFeedingSummaryResult result, NutritionGoal? goal)
        {
            // A goal of zero has no meaningful deviation to express (and no
            // division), so the classifier's body-weight target stands.
            if (goal?.DailyCalorieTarget is not > 0)
                return result;

            var target = (decimal)goal.DailyCalorieTarget.Value;
            var deviationPct = Math.Round(
                (result.ActualCalories - target) / target * 100m,
                2,
                MidpointRounding.AwayFromZero);

            return result with
            {
                TargetCalories = target,
                DeviationPct = deviationPct,
                Status = ToStatus(deviationPct)
            };
        }

        private static ClassifierFeedingStatus ToStatus(decimal deviationPct) => deviationPct switch
        {
            <= -ExtremeDeviationPct => ClassifierFeedingStatus.ExtremeUnderTarget,
            < -OnTargetDeviationPct => ClassifierFeedingStatus.UnderTarget,
            <= OnTargetDeviationPct => ClassifierFeedingStatus.OnTarget,
            < ExtremeDeviationPct => ClassifierFeedingStatus.OverTarget,
            _ => ClassifierFeedingStatus.ExtremeOverTarget
        };

        private static ClassifierFeedingSummaryResult FindResult(
            ClassifierFeedingSummaryResponse response, Guid petId)
        {
            var petIdText = petId.ToString("D");
            return response.Results.FirstOrDefault(result =>
                    string.Equals(result.PetId, petIdText, StringComparison.OrdinalIgnoreCase))
                ?? throw new ClassifierInvalidResponseException(
                    "The classifier returned a malformed response.",
                    validationReason: "results holds no entry for the requested petId");
        }

        private static int? ToAgeMonths(DateTime? birthDate)
        {
            if (birthDate is null)
                return null;

            var days = (DateTime.UtcNow - birthDate.Value).TotalDays;
            if (days < 0)
                return null;

            // The route caps age at 600 months; anything older is treated as
            // fully grown either way.
            return Math.Min((int)(days / AverageDaysPerMonth), MaxAgeMonths);
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
