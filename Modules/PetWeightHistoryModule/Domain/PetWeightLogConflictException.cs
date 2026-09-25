using smart_pet_care_api.Common.Api;

namespace smart_pet_care_api.Modules.PetWeightHistoryModule.Domain
{
    public class PetWeightLogConflictException(string message)
        : ConflictException(ErrorCodes.WeightLog.MeasurementTimeConflict, message)
    {
    }
}
