using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using smart_pet_care_api.Models;

namespace smart_pet_care_api.Modules.AuthModule.Repository
{
    public interface IAuthRepository
    {
        Task<RefreshToken?> GetByHashAsync(string tokenHash);
        Task AddAsync(RefreshToken token);

        /// <summary>
        /// Every live refresh token, uncapped. There used to be a second method taking
        /// the ten newest, which logout used — leaving an eleventh working for its full
        /// seven days after the user had signed out.
        /// </summary>
        Task<List<RefreshToken>> GetAllActiveByUserAsync(Guid userId);

        /// <summary>
        /// Revokes every live token in one rotation family, for a token presented after
        /// it was already spent. One statement, because the caller is about to refuse
        /// the request and has nothing to save.
        /// </summary>
        Task<int> RevokeFamilyAsync(Guid tokenFamilyId, DateTime revokedAt);

        Task AddConfirmationCodeAsync(EmailConfirmationCode code);
        Task<EmailConfirmationCode?> GetLatestConfirmationCodeAsync(Guid userId);
        Task<List<EmailConfirmationCode>> GetActiveConfirmationCodesAsync(Guid userId);

        Task AddPasswordResetTokenAsync(PasswordResetToken token);

        /// <summary>Tracked: the caller spends the token it finds.</summary>
        Task<PasswordResetToken?> GetPasswordResetTokenAsync(string tokenHash);

        Task<PasswordResetToken?> GetLatestPasswordResetTokenAsync(Guid userId);
        Task<List<PasswordResetToken>> GetActivePasswordResetTokensAsync(Guid userId);

        Task<int> SaveChangesAsync();
    }
}