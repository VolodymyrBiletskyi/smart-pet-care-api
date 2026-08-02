using System;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Models
{
    /// <summary>
    /// One AI review of a pet's daily feeding, as returned by the classifier's
    /// <c>feeding-summary</c> route. Only the two most recent analyses are kept
    /// per pet so a client can show the latest result next to the one before it.
    /// </summary>
    public class NutritionAnalysis
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid PetId { get; set; }

        /// <summary>The local day that was analysed.</summary>
        public DateOnly Date { get; set; }
        public int UtcOffsetMinutes { get; set; }

        public FeedingStatus Status { get; set; }

        /// <summary>
        /// The daily calorie need this analysis was graded against: the pet's
        /// nutrition goal when one sets a target above zero, otherwise the
        /// figure the classifier derived from the pet's body data.
        /// </summary>
        public decimal TargetCalories { get; set; }

        /// <summary>Calories the day's feeding logs actually added up to.</summary>
        public decimal ActualCalories { get; set; }

        /// <summary>Signed percentage away from <see cref="TargetCalories"/>.</summary>
        public decimal DeviationPct { get; set; }

        public string Disclaimer { get; set; } = null!;

        // Snapshot of the figures the analysis was based on. Feeding logs can
        // change after the fact, so a stored analysis must not be re-read
        // against a summary that has since moved.
        public int MealCount { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
