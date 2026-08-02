using System.ComponentModel.DataAnnotations;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.NutritionModule.DTOs.Requests
{
    /// <summary>
    /// Optional overrides for one analysis. Every property falls back to the
    /// pet's stored data, so omitting the body entirely reproduces the original
    /// behaviour: species, breed, weight and age come from the pet, and the
    /// products come from the analysed day's feeding logs.
    /// </summary>
    /// <remarks>
    /// The ranges mirror the limits the classifier enforces on
    /// <c>feeding-summary</c>. Breaking one there is a 422, which reaches the
    /// client as an opaque 502, so an out-of-range value is rejected here as a
    /// 400 that says which field was wrong.
    /// </remarks>
    public class NutritionAnalysisRequestDto
    {
        public AnimalSpecies? Species { get; set; }

        [StringLength(100)]
        public string? Breed { get; set; }

        [Range(0.0001, 500.0, ErrorMessage = "WeightKg must be above 0 and at most 500.")]
        public decimal? WeightKg { get; set; }

        [Range(0, 600)]
        public int? AgeMonths { get; set; }

        /// <summary>
        /// What the pet ate. An empty array is not the same as omitting the
        /// property: it grades the day as having eaten nothing, whereas
        /// omitting it reads the day's feeding logs.
        /// </summary>
        [MaxLength(100, ErrorMessage = "At most 100 products can be analysed at once.")]
        public List<NutritionAnalysisProductDto>? Products { get; set; }
    }

    public class NutritionAnalysisProductDto
    {
        [Required]
        [StringLength(200, MinimumLength = 1)]
        public string Name { get; set; } = null!;

        [Range(0.0, 20000.0)]
        public decimal Calories { get; set; }
    }
}
