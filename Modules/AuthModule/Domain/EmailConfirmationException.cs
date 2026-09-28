using smart_pet_care_api.Common.Api;

namespace smart_pet_care_api.Modules.AuthModule.Domain
{
    public enum EmailConfirmationError
    {
        CodeInvalid,
        CodeExpired,
        AlreadyConfirmed,
        TooManyAttempts,
        ResendTooSoon,
        EmailNotConfirmed
    }

    /// <summary>
    /// Carries its own status and alias like any other <see cref="AppException"/>.
    /// The aliases are screaming case because the mobile client already branches
    /// on the exact strings — see the note on
    /// <see cref="ErrorCodes.EmailConfirmation"/>.
    /// </summary>
    public class EmailConfirmationException(EmailConfirmationError error)
        : AppException(CodeFor(error), StatusFor(error), MessageFor(error))
    {
        public EmailConfirmationError Error { get; } = error;

        public string ErrorCode => Code;

        private static string CodeFor(EmailConfirmationError error) => error switch
        {
            EmailConfirmationError.CodeInvalid => ErrorCodes.EmailConfirmation.CodeInvalid,
            EmailConfirmationError.CodeExpired => ErrorCodes.EmailConfirmation.CodeExpired,
            EmailConfirmationError.AlreadyConfirmed => ErrorCodes.EmailConfirmation.AlreadyConfirmed,
            EmailConfirmationError.TooManyAttempts => ErrorCodes.EmailConfirmation.TooManyAttempts,
            EmailConfirmationError.ResendTooSoon => ErrorCodes.EmailConfirmation.ResendTooSoon,
            EmailConfirmationError.EmailNotConfirmed => ErrorCodes.EmailConfirmation.NotConfirmed,
            _ => ErrorCodes.EmailConfirmation.CodeInvalid
        };

        private static int StatusFor(EmailConfirmationError error) => error switch
        {
            EmailConfirmationError.AlreadyConfirmed => StatusCodes.Status409Conflict,
            EmailConfirmationError.CodeExpired => StatusCodes.Status410Gone,
            EmailConfirmationError.TooManyAttempts => StatusCodes.Status429TooManyRequests,
            EmailConfirmationError.ResendTooSoon => StatusCodes.Status429TooManyRequests,
            EmailConfirmationError.EmailNotConfirmed => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status400BadRequest
        };

        private static string MessageFor(EmailConfirmationError error) => error switch
        {
            EmailConfirmationError.CodeInvalid => "Confirmation code is not valid",
            EmailConfirmationError.CodeExpired => "Confirmation code has expired, request a new one",
            EmailConfirmationError.AlreadyConfirmed => "Email is already confirmed",
            EmailConfirmationError.TooManyAttempts => "Too many failed attempts, request a new code",
            EmailConfirmationError.ResendTooSoon => "A code was sent recently, wait before requesting another",
            EmailConfirmationError.EmailNotConfirmed => "Email is not confirmed yet",
            _ => "Confirmation code is not valid"
        };
    }
}
