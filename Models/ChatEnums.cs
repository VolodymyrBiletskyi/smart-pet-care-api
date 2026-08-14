using System.Text.Json;
using System.Text.Json.Serialization;

namespace smart_pet_care_api.Models;

[JsonConverter(typeof(SnakeCaseLowerPetTypeJsonConverter))]
public enum PetType
{
    Dog,
    Cat,
    Rabbit,
    Hamster,
    GuineaPig,
    Bird,
    Fish,
    Turtle,
    Other
}

public sealed class SnakeCaseLowerPetTypeJsonConverter()
    : JsonStringEnumConverter<PetType>(JsonNamingPolicy.SnakeCaseLower);

[JsonConverter(typeof(JsonStringEnumConverter<ChatMessageRole>))]
public enum ChatMessageRole
{
    [JsonStringEnumMemberName("user")]
    User,

    [JsonStringEnumMemberName("assistant")]
    Assistant
}

[JsonConverter(typeof(JsonStringEnumConverter<ChatMessageStatus>))]
public enum ChatMessageStatus
{
    Pending,
    Completed,
    FailedRetryable,
    FailedNonRetryable
}
