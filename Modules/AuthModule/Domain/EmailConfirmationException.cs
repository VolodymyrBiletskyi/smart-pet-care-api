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

    public class EmailConfirmationException : Exception
    {
        public EmailConfirmationError Error { get; }

        public EmailConfirmationException(EmailConfirmationError error) : base(MessageFor(error))
        {
            Error = error;
        }

        public string ErrorCode => Error switch
        {
            EmailConfirmationError.CodeInvalid => "CONFIRMATION_CODE_INVALID",
            EmailConfirmationError.CodeExpired => "CONFIRMATION_CODE_EXPIRED",
            EmailConfirmationError.AlreadyConfirmed => "EMAIL_ALREADY_CONFIRMED",
            EmailConfirmationError.TooManyAttempts => "CONFIRMATION_TOO_MANY_ATTEMPTS",
            EmailConfirmationError.ResendTooSoon => "CONFIRMATION_RESEND_TOO_SOON",
            EmailConfirmationError.EmailNotConfirmed => "EMAIL_NOT_CONFIRMED",
            _ => "CONFIRMATION_CODE_INVALID"
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
