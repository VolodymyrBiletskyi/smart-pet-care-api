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
        Task<List<RefreshToken>> GetActiveByUserAsync(Guid userId);

        /// <summary>
        /// Every live refresh token, uncapped. <see cref="GetActiveByUserAsync"/> takes
        /// the ten newest, which is enough to sign a session out; a password reset has to
        /// end all of them or the eleventh keeps working.
        /// </summary>
        Task<List<RefreshToken>> GetAllActiveByUserAsync(Guid userId);

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