using System.ComponentModel.DataAnnotations;

namespace smart_pet_care_api.Modules.ChatModule.DTOs;

public sealed record CreateChatSessionRequest
{
    [Required]
    public Guid PetId { get; init; }
}
