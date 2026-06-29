using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Options;

namespace smart_pet_care_api.Modules.NotificationModule.Config
{
    /// <summary>
    /// Initializes the default <see cref="FirebaseApp"/> exactly once for the process.
    /// Registered as a singleton; inject it anywhere FCM is used to guarantee init ordering.
    /// </summary>
    public sealed class FirebaseInitializer
    {
        private static readonly object Gate = new();

        public FirebaseApp App { get; }
        public bool IsConfigured { get; }

        public FirebaseInitializer(IOptions<FirebaseOptions> options, ILogger<FirebaseInitializer> logger)
        {
            lock (Gate)
            {
                if (FirebaseApp.DefaultInstance is not null)
                {
                    App = FirebaseApp.DefaultInstance;
                    IsConfigured = true;
                    return;
                }

                var credential = LoadCredential(options.Value, logger);
                if (credential is null)
                {
                    IsConfigured = false;
                    App = null!;
                    return;
                }

                App = FirebaseApp.Create(new AppOptions
                {
                    Credential = credential,
                    ProjectId = options.Value.ProjectId
                });
                IsConfigured = true;
                logger.LogInformation("Firebase Admin SDK initialized for project {ProjectId}", options.Value.ProjectId);
            }
        }

        private static GoogleCredential? LoadCredential(FirebaseOptions options, ILogger logger)
        {
            var raw = options.ServiceAccountJson;
            if (string.IsNullOrWhiteSpace(raw))
            {
                logger.LogWarning("Firebase ServiceAccountJson not configured; push notifications are disabled.");
                return null;
            }

            try
            {
#pragma warning disable CS0618
                if (raw.TrimStart().StartsWith('{'))
                    return GoogleCredential.FromJson(raw);

                if (File.Exists(raw))
                    return GoogleCredential.FromFile(raw);
#pragma warning restore CS0618

                logger.LogError("Firebase ServiceAccountJson is neither valid JSON nor an existing file path.");
                return null;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to load Firebase service-account credential.");
                return null;
            }
        }
    }
}
