using smart_pet_care_api.Common.Patching;

namespace smart_pet_care_api.Modules.PetWeightHistoryModule.DTOs.Requests
{
    public class PatchPetWeightLogDto
    {
        public PatchField<decimal> WeightKg { get; set; }
        public PatchField<DateTime> MeasuredAt { get; set; }
        public PatchField<string?> Notes { get; set; }
    }
}
