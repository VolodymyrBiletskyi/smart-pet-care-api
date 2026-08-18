namespace smart_pet_care_api.Models;

public sealed class ChatMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public ChatMessageRole Role { get; set; }
    public ChatMessageStatus? Status { get; set; }
    public Guid? ClientMessageId { get; set; }
    public Guid? SourceMessageId { get; set; }
    public string? ClassifierResponseJson { get; set; }
    public string Content { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
