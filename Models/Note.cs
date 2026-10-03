namespace smart_pet_care_api.Models
{
    public class Note
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid PetId { get; set; }

        public string Title { get; set; } = null!;
        public string Content { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Set to CreatedAt on insert rather than left null, so a client can sort
        // by it without a fallback and the list order is one column.
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
