using FirebaseAdmin.Messaging;
using Polly;
using Polly.Retry;

namespace smart_pet_care_api.Modules.NotificationModule.Config
{
    public sealed class FcmRetryPolicy
    {
        public AsyncRetryPolicy Policy { get; }

        public FcmRetryPolicy(ILogger<FcmRetryPolicy> logger)
        {
            Policy = Polly.Policy
                .Handle<FirebaseMessagingException>(IsTransient)
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                    onRetry: (ex, delay, attempt, _) =>
                        logger.LogWarning(ex,
                            "FCM send failed (attempt {Attempt}); retrying in {Delay}s",
                            attempt, delay.TotalSeconds));
        }

        public static bool IsTokenInvalid(FirebaseMessagingException ex) =>
            ex.MessagingErrorCode is MessagingErrorCode.Unregistered
                or MessagingErrorCode.InvalidArgument;

        private static bool IsTransient(FirebaseMessagingException ex) => !IsTokenInvalid(ex);
    }
}
