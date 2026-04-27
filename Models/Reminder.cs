using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Models
{
    public class Reminder
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid PetId { get; set; }

        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public ReminderType Type { get; set; }

        public ReminderStatus Status { get; set; } = ReminderStatus.Active;

        public DateTime StartAt { get; set; }
        public DateTime? NextTriggerAt { get; set; }
        public DateTime? EndAt { get; set; }

        public Frequency Frequency { get; set; } = Frequency.Once;
        public int Interval { get; set; } = 1;

        public bool IsSystemGenerated { get; set; } = false;
        public SourceType SourceType { get; set; }
        public Guid? SourceId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public ICollection<ReminderRun> ReminderRuns { get; set; } = new List<ReminderRun>();


    }
}