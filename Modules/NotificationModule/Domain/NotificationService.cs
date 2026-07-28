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
        // Channel ids are versioned (-v1) because Android freezes a channel's sound at creation
        // time; changing a sound later requires shipping a new channel id, not editing this one.
        private static readonly Dictionary<AnimalSpecies, (string ChannelId, string Sound)> SpeciesChannels = new()
        {
            [AnimalSpecies.Dog] = ("pet-reminders-dog-v1", "dog.wav"),
            [AnimalSpecies.Cat] = ("pet-reminders-cat-v1", "cat.wav"),
            [AnimalSpecies.GuineaPig] = ("pet-reminders-guinea-pig-v1", "guinea_pig.wav"),
            [AnimalSpecies.Bird] = ("pet-reminders-bird-v1", "bird.wav"),
            [AnimalSpecies.Fish] = ("pet-reminders-fish-v1", "fish.wav")
        };

        // Unknown, Rabbit, Hamster, Turtle and Other fall through to this, as does any species
        // added to the enum later without a bundled sound.
        private static readonly (string ChannelId, string Sound) DefaultChannel = ("default", "default");

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

        public async Task<bool> SendReminderNotificationAsync(Reminder reminder, DateTime scheduledFor, CancellationToken ct)
        {
            if (!_firebase.IsConfigured)
            {
                _logger.LogDebug("Firebase not configured; skipping push for reminder {ReminderId}", reminder.Id);
                return false;
            }

            var pet = await _petRepo.GetByIdAsync(reminder.PetId);
            if (pet is null)
            {
                _logger.LogWarning("Pet {PetId} not found for reminder {ReminderId}; skipping push",
                    reminder.PetId, reminder.Id);
                return false;
            }

            var tokens = await _tokenRepo.GetByUserIdAsync(pet.UserId);
            if (tokens.Count == 0)
            {
                _logger.LogDebug("No device tokens for user {UserId}; nothing to send", pet.UserId);
                return false;
            }

            var (title, body) = BuildContent(reminder, pet.Name);

            // Resolved once per reminder rather than per token: every device for this user is
            // being notified about the same pet, so the channel cannot differ between messages.
            var channel = ResolveChannel(pet.Species);

            var data = new Dictionary<string, string>
            {
                ["reminderId"] = reminder.Id.ToString(),
                ["petId"] = reminder.PetId.ToString(),
                ["petSpecies"] = pet.Species.ToString(),
                ["reminderType"] = reminder.Type.ToString(),
                ["scheduledAt"] = scheduledFor.ToString("o"),
                ["channelId"] = channel.ChannelId
            };

            return tokens.Count == 1
                ? await SendSingleAsync(tokens[0], title, body, data, channel, ct)
                : await SendBatchAsync(tokens, title, body, data, channel, ct);
        }

        private static (string ChannelId, string Sound) ResolveChannel(AnimalSpecies species) =>
            SpeciesChannels.TryGetValue(species, out var channel) ? channel : DefaultChannel;

        private async Task<bool> SendSingleAsync(
            DeviceToken token, string title, string body,
            IReadOnlyDictionary<string, string> data,
            (string ChannelId, string Sound) channel, CancellationToken ct)
        {
            var message = BuildMessage(token.Token, title, body, data, channel);

            try
            {
                await _retry.Policy.ExecuteAsync(
                    _ => FirebaseMessaging.DefaultInstance.SendAsync(message, ct), ct);
                return true;
            }
            catch (FirebaseMessagingException ex) when (FcmRetryPolicy.IsTokenInvalid(ex))
            {
                _logger.LogInformation("Removing invalid device token ({Code})", ex.MessagingErrorCode);
                await _tokenRepo.RemoveByTokensAsync(new[] { token.Token });
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send push notification after retries");
                return false;
            }
        }

        private async Task<bool> SendBatchAsync(
            IReadOnlyList<DeviceToken> tokens, string title, string body,
            IReadOnlyDictionary<string, string> data,
            (string ChannelId, string Sound) channel, CancellationToken ct)
        {
            var messages = tokens
                .Select(t => BuildMessage(t.Token, title, body, data, channel))
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
                return false;
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

            return response.SuccessCount > 0;
        }

        private static Message BuildMessage(
            string token, string title, string body, IReadOnlyDictionary<string, string> data,
            (string ChannelId, string Sound) channel)
        {
            return new Message
            {
                Token = token,
                Notification = new Notification { Title = title, Body = body },
                Data = new Dictionary<string, string>(data),
                Android = new AndroidConfig
                {
                    Priority = Priority.High,
                    Notification = new AndroidNotification
                    {
                        ChannelId = channel.ChannelId,
                        Sound = channel.Sound
                    }
                },
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
