using FirebaseAdmin.Messaging;
using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.NotificationModule.Config;
using smart_pet_care_api.Modules.NotificationModule.Repository;
using smart_pet_care_api.Modules.PetModule.Repository;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.NotificationModule.Domain
{
    public class NotificationService : INotificationService
    {
        private readonly IDeviceTokenRepository _tokenRepo;
        private readonly IPetRepository _petRepo;
        private readonly FcmRetryPolicy _retry;
        private readonly FirebaseInitializer _firebase;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(
            IDeviceTokenRepository tokenRepo,
            IPetRepository petRepo,
            FcmRetryPolicy retry,
            FirebaseInitializer firebase,
            ILogger<NotificationService> logger)
        {
            _tokenRepo = tokenRepo;
            _petRepo = petRepo;
            _retry = retry;
            _firebase = firebase;
            _logger = logger;
        }

        public async Task SendReminderNotificationAsync(Reminder reminder, CancellationToken ct)
        {
            if (!_firebase.IsConfigured)
            {
                _logger.LogDebug("Firebase not configured; skipping push for reminder {ReminderId}", reminder.Id);
                return;
            }

            var pet = await _petRepo.GetByIdAsync(reminder.PetId);
            if (pet is null)
            {
                _logger.LogWarning("Pet {PetId} not found for reminder {ReminderId}; skipping push",
                    reminder.PetId, reminder.Id);
                return;
            }

            var tokens = await _tokenRepo.GetByUserIdAsync(pet.UserId);
            if (tokens.Count == 0)
            {
                _logger.LogDebug("No device tokens for user {UserId}; nothing to send", pet.UserId);
                return;
            }

            var (title, body) = BuildContent(reminder, pet.Name);
            var data = new Dictionary<string, string>
            {
                ["reminderId"] = reminder.Id.ToString(),
                ["petId"] = reminder.PetId.ToString(),
                ["reminderType"] = reminder.Type.ToString(),
                ["scheduledAt"] = (reminder.NextTriggerAt ?? reminder.StartAt).ToString("o")
            };

            if (tokens.Count == 1)
                await SendSingleAsync(tokens[0], title, body, data, ct);
            else
                await SendBatchAsync(tokens, title, body, data, ct);
        }

        private async Task SendSingleAsync(
            DeviceToken token, string title, string body,
            IReadOnlyDictionary<string, string> data, CancellationToken ct)
        {
            var message = BuildMessage(token.Token, title, body, data);

            try
            {
                await _retry.Policy.ExecuteAsync(
                    _ => FirebaseMessaging.DefaultInstance.SendAsync(message, ct), ct);
            }
            catch (FirebaseMessagingException ex) when (FcmRetryPolicy.IsTokenInvalid(ex))
            {
                _logger.LogInformation("Removing invalid device token ({Code})", ex.MessagingErrorCode);
                await _tokenRepo.RemoveByTokensAsync(new[] { token.Token });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send push notification after retries");
            }
        }

        private async Task SendBatchAsync(
            IReadOnlyList<DeviceToken> tokens, string title, string body,
            IReadOnlyDictionary<string, string> data, CancellationToken ct)
        {
            var messages = tokens
                .Select(t => BuildMessage(t.Token, title, body, data))
                .ToList();

            BatchResponse response;
            try
            {
                response = await _retry.Policy.ExecuteAsync(
                    _ => FirebaseMessaging.DefaultInstance.SendEachAsync(messages, ct), ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FCM batch send failed after retries for {Count} tokens", messages.Count);
                return;
            }

            var staleTokens = new List<string>();
            for (var i = 0; i < response.Responses.Count; i++)
            {
                var result = response.Responses[i];
                if (result.IsSuccess) continue;

                if (result.Exception is FirebaseMessagingException fx && FcmRetryPolicy.IsTokenInvalid(fx))
                    staleTokens.Add(tokens[i].Token);
                else
                    _logger.LogWarning(result.Exception, "FCM send failed for a device token");
            }

            if (staleTokens.Count > 0)
            {
                _logger.LogInformation("Removing {Count} invalid device tokens", staleTokens.Count);
                await _tokenRepo.RemoveByTokensAsync(staleTokens);
            }
        }

        private static Message BuildMessage(
            string token, string title, string body, IReadOnlyDictionary<string, string> data)
        {
            return new Message
            {
                Token = token,
                Notification = new Notification { Title = title, Body = body },
                Data = new Dictionary<string, string>(data),
                Android = new AndroidConfig { Priority = Priority.High },
                Apns = new ApnsConfig
                {
                    Aps = new Aps { Sound = "default" }
                }
            };
        }

        private static (string Title, string Body) BuildContent(Reminder reminder, string petName)
        {
            var title = reminder.Type switch
            {
                ReminderType.Feeding => $"Time to feed {petName}!",
                ReminderType.Activity => $"Activity time for {petName}",
                ReminderType.Medication => $"Medication reminder for {petName}",
                ReminderType.Vaccination => $"Vaccination reminder for {petName}",
                ReminderType.ParasiteTreatment => $"Parasite treatment for {petName}",
                ReminderType.VetVisit => $"Vet visit for {petName}",
                ReminderType.Grooming => $"Grooming time for {petName}",
                _ => $"Reminder for {petName}"
            };

            var body = !string.IsNullOrWhiteSpace(reminder.Description)
                ? reminder.Description!
                : reminder.Title;

            return (title, body);
        }
    }
}
