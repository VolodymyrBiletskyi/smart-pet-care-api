using smart_pet_care_api.Common.Api;
using System.ComponentModel.DataAnnotations;

namespace smart_pet_care_api.Modules.ChatModule.DTOs;

public sealed record CreateChatSessionRequest
{
    [Required(ErrorMessage = ErrorCodes.Chat.PetIdRequired)]
    public Guid PetId { get; init; }
}
