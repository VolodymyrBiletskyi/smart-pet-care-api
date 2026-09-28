using smart_pet_care_api.Common.Api;
using System.ComponentModel.DataAnnotations;

namespace smart_pet_care_api.Modules.ChatModule.DTOs;

public sealed record PostSessionMessageRequest
{
    [Required(ErrorMessage = ErrorCodes.Chat.ClientMessageIdRequired)]
    public Guid? ClientMessageId { get; init; }

    [Required(ErrorMessage = ErrorCodes.Chat.MessageTextRequired)]
    [StringLength(4000, MinimumLength = 1, ErrorMessage = ErrorCodes.Chat.MessageTextTooLong)]
    public string Text { get; init; } = null!;
}
