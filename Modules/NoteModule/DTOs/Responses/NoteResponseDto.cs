namespace smart_pet_care_api.Modules.NoteModule.DTOs.Responses
{
    public class NoteResponseDto
    {
        public Guid Id { get; set; }
        public Guid PetId { get; set; }
        public string Title { get; set; } = null!;
        public string Content { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
