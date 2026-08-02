using System;
using System.Collections.Generic;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Models
{
    /// <summary>
    /// One AI review of a pet's daily nutrition summary. Only the two most
    /// recent analyses are kept per pet so a client can show the latest result
    /// next to the one before it.
    /// </summary>
    public class NutritionAnalysis
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid PetId { get; set; }

        /// <summary>The local day that was analysed.</summary>
        public DateOnly Date { get; set; }
        public int UtcOffsetMinutes { get; set; }

        public NutritionGrade Grade { get; set; }

        /// <summary>Overall quality of the day, from 0 to 100.</summary>
        public int Score { get; set; }

        public string Summary { get; set; } = null!;
        public List<string> Advice { get; set; } = new();
        public string Disclaimer { get; set; } = null!;

        // Snapshot of the figures the analysis was based on. Feeding logs can
        // change after the fact, so a stored analysis must not be re-read
        // against a summary that has since moved.
        public int MealCount { get; set; }
        public int TotalCalories { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
