namespace smart_pet_care_api.Models;

public sealed class ChatSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid PetId { get; set; }
    public PetType PetType { get; set; }
    public string? SymptomSummary { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}
