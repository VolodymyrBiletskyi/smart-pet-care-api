namespace smart_pet_care_api.Modules.ActivityModule.DTOs.Requests
{
    public class CreateSleepLogDto
    {
        /// <summary>
        /// The day slept. Only the date part is kept — a time of day would suggest a
        /// precision nobody hand-typing "slept about twelve hours" has.
        /// </summary>
        public DateTime SleepDate { get; set; }

        public decimal Hours { get; set; }

        public string? Note { get; set; }
    }
}
