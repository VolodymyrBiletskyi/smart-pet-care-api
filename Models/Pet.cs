using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Models
{
    public class Pet
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid UserId { get; set; }
        public string? PhotoUrl { get; set; }
        public string? PhotoPublicId { get; set; }

        public string Name { get; set; } = null!;
        public AnimalSpecies Species { get; set; }
        public string? Breed { get; set; }
        public DateTime? BirthDate { get; set; }
        public Sex Sex { get; set; } = Sex.Unknown;
        public decimal? WeightKg { get; set; }
        public List<string>? BehavioralNotes { get; set; }
        public List<string>? Allergies { get; set; }
        public List<string>? ChronicConditions { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }



        public ICollection<PetCondition> PetConditions { get; set; } = new List<PetCondition>();
        public ICollection<PetMedication> PetMedications { get; set; } = new List<PetMedication>();
        public ICollection<PetFile> PetFiles { get; set; } = new List<PetFile>();
        public ICollection<PetEvent> PetEvents { get; set; } = new List<PetEvent>();
        public ICollection<ActivityDaily> ActivityDailies { get; set; } = new List<ActivityDaily>();
        public ICollection<FeedingLog> FeedingLogs { get; set; } = new List<FeedingLog>();
        public ICollection<HealthRecord> HealthRecords { get; set; } = new List<HealthRecord>();
        public ICollection<JournalEntry> JournalEntries { get; set; } = new List<JournalEntry>();
        public ICollection<PetWeightLog> WeightLogs { get; set; } = new List<PetWeightLog>();
        public ICollection<Reminder> Reminders { get; set; } = new List<Reminder>();
    }
}
