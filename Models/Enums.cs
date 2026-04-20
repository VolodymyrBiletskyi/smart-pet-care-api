using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace smart_pet_care_api.Models
{
    public class Enums
    {
        public enum Sex
        {
            Male,
            Female
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
        public enum ReminderType
        {
            Feeding,
            Activity,
            Medication,
            Vaccination,
            ParasiteTreatment,
            vetVisit,
            Grooming,
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
        public enum AiSessionType
        {
            Health,
            PetQuestions,
            General
        }

        public enum AiSessionStatus
        {
            Active,
            Closed,
            Archived
        }

        public enum AiMessageRole
        {
            User,
            Assistant,
            System
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

    }
}