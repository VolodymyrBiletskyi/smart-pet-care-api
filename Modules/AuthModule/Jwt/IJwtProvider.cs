namespace smart_pet_care_api.Modules.AuthModule.Jwt
{
    public interface IJwtProvider
    {
        /// <summary>
        /// The id and the address, rather than a user, because those are the token's only
        /// two claims — and a caller that has just read them off a projection should not
        /// have to load the row to get a session.
        /// </summary>
        string GenerateAccessToken(Guid userId, string email);
        string GenerateRefreshToken();
        string HashToken(string token);

        int AccessTokenMinutes { get; }
    }
}
