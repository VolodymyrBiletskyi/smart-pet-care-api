namespace smart_pet_care_api.Modules.UserModule.Repository
{
    /// <summary>
    /// Read models for the paths that only need a few of a user's columns.
    /// <c>Users.AvatarData</c> holds up to a megabyte of image bytes, and loading the whole
    /// row to build a three-hundred-byte response moved that blob on every login and every
    /// profile read. Each of these is projected in SQL, so the column is never selected.
    /// </summary>
    public sealed record UserCredentials(
        Guid Id,
        string Email,
        string? PasswordHash,
        bool EmailConfirmed);

    /// <summary>
    /// <paramref name="HasAvatar"/> rather than the bytes: the response only needs to know
    /// whether to publish an avatar URL, which is the one thing the blob was being read for.
    /// </summary>
    public sealed record UserProfileRow(
        Guid Id,
        string Email,
        string? DisplayName,
        string? PhoneNumber,
        string? AvatarUrl,
        bool HasAvatar,
        bool TermsAccepted,
        DateTime CreatedAt,
        DateTime? UpdatedAt);

    /// <summary>The avatar itself, for the one route that serves the bytes.</summary>
    public sealed record UserAvatar(byte[]? Data, string? ContentType);
}
