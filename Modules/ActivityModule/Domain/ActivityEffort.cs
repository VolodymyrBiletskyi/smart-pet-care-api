using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.ActivityModule.Domain
{
    /// <summary>
    /// Turns a logged session into the one number the wellness score cares about: active
    /// minutes. The activity type is deliberately absent from this maths — it is a label,
    /// and a walk done at a run is not a different activity, it is a harder one.
    /// </summary>
    public static class ActivityEffort
    {
        /// <summary>
        /// Share of the elapsed time that counts as active. A lead-and-sniff walk is mostly
        /// standing still; a swim is not.
        /// </summary>
        public static decimal WeightFor(ActivityIntensity intensity) => intensity switch
        {
            ActivityIntensity.Low => 0.4m,
            ActivityIntensity.Moderate => 0.7m,
            ActivityIntensity.High => 1.0m,
            _ => 0m
        };

        /// <summary>
        /// What a pet is doing when the caller named the activity but not how hard it was.
        /// A guess is fair here because the type carries most of the answer, and the
        /// alternative — a second required field — is how logging gets abandoned.
        /// </summary>
        public static ActivityIntensity DefaultIntensityFor(ActivityType? type) => type switch
        {
            ActivityType.Walk => ActivityIntensity.Low,
            ActivityType.Run or ActivityType.Swimming => ActivityIntensity.High,
            _ => ActivityIntensity.Moderate
        };

        public static int? ActiveMinutes(int? durationMinutes, ActivityIntensity? intensity)
        {
            if (durationMinutes is not { } duration || intensity is not { } value)
                return null;

            return (int)Math.Round(duration * WeightFor(value), MidpointRounding.AwayFromZero);
        }
    }
}
