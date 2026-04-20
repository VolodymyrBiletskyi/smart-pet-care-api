using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace smart_pet_care_api.Models
{
    public class PetMedicationScheduleTime
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid PetMedicationId { get; set; }
        public PetMedication PetMedication { get; set; } = null!;

        public TimeSpan TimeOfDay { get; set; }

    }
}