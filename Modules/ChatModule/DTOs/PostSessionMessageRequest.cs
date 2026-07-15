using System.ComponentModel.DataAnnotations;

namespace smart_pet_care_api.Modules.ChatModule.DTOs;

public sealed record PostSessionMessageRequest
{
    [Required]
    [StringLength(4000, MinimumLength = 1)]
    public string Text { get; init; } = null!;
}
