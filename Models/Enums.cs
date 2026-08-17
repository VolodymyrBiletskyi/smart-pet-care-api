using System.Text.Json.Serialization;

namespace smart_pet_care_api.Models
{
    public class Enums
    {
        public enum Sex
        {
            Unknown = 0,
            Male = 1,
            Female = 2
        }
        public enum Status
        {
            Open,
            Addressed,
            Dismissed
        }
        public enum CareType
        {
            Vaccination,
            AntiParasiteTreatment,
            Grooming,
            Other
        }
        public enum VisitType
        {
            Veterinary,
            Grooming,
        }
        /// <summary>
        /// Stored as an integer, so new values must be appended — inserting in the
        /// middle shifts every later value and rewrites the meaning of existing rows.
        /// The grooming values below are labels only: recurrence behaviour lives in
        /// <see cref="RecalcStrategy"/>, not in the type.
        /// </summary>
        public enum ReminderType
        {
            Feeding,
            Activity,
            Medication,
            Vaccination,
            ParasiteTreatment,
            VetVisit,
            /// <summary>Unspecified grooming; kept as the bucket for rows created before the labels below existed.</summary>
            Grooming,
            Weighing,
            Deworming,
            Bathing,
            Brushing,
            EarCleaning,
            NailTrimming,
            PawCare,
            TeethCleaning,
        }

        /// <summary>
        /// How the next trigger is derived once an occurrence is completed.
        /// </summary>
        public enum RecalcStrategy
        {
            /// <summary>Ignore the completion date and keep following the calendar.</summary>
            Calendar = 0,

            /// <summary>
            /// Next trigger is the completion date plus the interval, exactly. Used where the
            /// interval is a safety property (antiparasitics protect for N days from the dose).
            /// </summary>
            FromCompletion = 1,

            /// <summary>
            /// Completion date plus the interval, then moved forward to the nearest selected
            /// weekday. Keeps habits like "bathing on Saturdays" from drifting while never
            /// shortening the interval.
            /// </summary>
            FromCompletionAlignedToWeekday = 2,
        }
        public enum Frequency
        {
            Once,
            Daily,
            Weekly,
            Monthly,
            Yearly,
        }
        public enum ReminderStatus
        {
            Active,
            Completed,
            Missed,
            Cancelled,
        }

        public enum RepeatType
        {
            Weekly = 0,
            Monthly = 1,
            Once = 2,
            Daily = 3,
        }

        public enum ReminderRunStatus
        {
            Pending,
            Sent,
            Delivered,
            Failed,
            Missed,
            Completed,
            Cancelled
        }
        public enum SourceType
        {
            Manual,
            Medication,
            Visit,
            CareRecord,
        }
        public enum AuthProvider
        {
            Google,
            Facebook,
            Apple,
            Twitter,
            Other
        }

        public enum PetEventType
        {
            VetVisit,
            Grooming,
            Vaccination,
            ParasiteTreatment,
            Medication,
            Feeding,
            Walk,
            Checkup,
            Custom
        }

        public enum PetEventStatus
        {
            Planned,
            Completed,
            Cancelled,
            Missed
        }

        public enum PetEventPriority
        {
            Low,
            Normal,
            High,
            Urgent
        }
        public enum FoodType
        {
            DryFood,
            WetFood,
            Homemade,
            Treat,
            Supplement,
            Other
        }

        public enum PortionUnit
        {
            Gram,
            Milliliter,
            Cup,
            Piece
        }

        /// <summary>
        /// How a day's logged calories compare with the classifier's target for
        /// the pet. Mirrors the classifier's <c>feeding-summary</c> statuses.
        /// </summary>
        public enum FeedingStatus
        {
            ExtremeUnderTarget,
            UnderTarget,
            OnTarget,
            OverTarget,
            ExtremeOverTarget
        }

        public enum ActivitySource
        {
            Manual,
            Device,
            FitBark,
            AppleHealth,
            GoogleFit,
            Mock
        }

        public enum ConditionType
        {
            Chronic,
            Acute,
            Other
        }

        public enum FileType
        {
            Photo,
            Document,
            Other
        }

        public enum DaysOfWeek
        {
            Sunday,
            Monday,
            Tuesday,
            Wednesday,
            Thursday,
            Friday,
            Saturday
        }

        public enum DevicePlatform
        {
            Android,
            iOS
        }

        public enum HealthRecordType
        {
            Vaccination,
            Deworming,
            AntiParasiteTreatment,
            Medication,
            VetVisit,
            Surgery,
            HealthNote,
            Symptom
        }

        public enum JournalEntryType
        {
            Observation,
            Symptom,
            BehaviorChange,
            AppetiteChange,
            PreVetNote,
            Other
        }

        public enum JournalEntrySeverity
        {
            Mild,
            Moderate,
            Severe
        }

        public enum SymptomType
        {
            // General
            Fever,
            Lethargy,
            WeightLoss,
            WeightGain,
            Dehydration,
            Pain,

            // Digestive
            Vomiting,
            Diarrhea,
            Constipation,
            LossOfAppetite,
            IncreasedAppetite,

            // Respiratory
            Coughing,
            Sneezing,
            NasalDischarge,
            DifficultyBreathing,

            // Skin & coat
            Itching,
            HairLoss,
            Swelling,

            // Urinary
            IncreasedThirst,
            FrequentUrination,

            // Eyes & ears
            EyeDischarge,
            EarDischarge,

            // Neurological & mobility
            Seizure,
            Limping,

            // Other
            Bleeding,
            Other
        }

        public enum AnimalSpecies
        {
            Unknown,
            Dog,
            Cat,
            Rabbit,
            Hamster,
            GuineaPig,
            Bird,
            Fish,
            Turtle,
            Other
        }

    }
}
