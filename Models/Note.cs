namespace smart_pet_care_api.Models
{
    public class Note
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid PetId { get; set; }

        // Either one may be absent -- a one-line title with nothing under it and
        // a body with no heading are both notes. Only a row with neither is not.
        public string? Title { get; set; }
        public string? Content { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Set to CreatedAt on insert rather than left null, so a client can sort
        // by it without a fallback and the list order is one column.
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
