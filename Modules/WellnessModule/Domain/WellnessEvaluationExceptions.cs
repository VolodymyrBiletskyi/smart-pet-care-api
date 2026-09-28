using smart_pet_care_api.Common.Api;

namespace smart_pet_care_api.Modules.WellnessModule.Domain;

/// <summary>
/// Deliberately not an <c>InvalidOperationException</c> any more: that is what
/// the framework throws too, and inheriting from it is how a stray EF failure
/// used to pass for a wellness answer.
/// </summary>
public sealed class WellnessInsufficientDataException()
    : UnprocessableException(
        ErrorCodes.Wellness.InsufficientData,
        "There is not enough information to evaluate wellness");
