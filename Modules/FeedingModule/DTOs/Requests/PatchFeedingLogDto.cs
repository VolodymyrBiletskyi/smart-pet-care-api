using smart_pet_care_api.Common.Patching;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.FeedingModule.DTOs.Requests
{
    public class PatchFeedingLogDto
    {
        public PatchField<DateTime> FedAt { get; set; }
        public PatchField<FoodType> FoodType { get; set; }
        public PatchField<string?> FoodName { get; set; }
        public PatchField<decimal?> PortionAmount { get; set; }
        public PatchField<PortionUnit?> PortionUnit { get; set; }
        public PatchField<int?> ApproxCalories { get; set; }
        public PatchField<string?> Notes { get; set; }
    }
}
