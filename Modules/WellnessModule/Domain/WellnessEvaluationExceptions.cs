namespace smart_pet_care_api.Modules.WellnessModule.Domain;

public sealed class WellnessInsufficientDataException()
    : InvalidOperationException("There is not enough information to evaluate wellness");
