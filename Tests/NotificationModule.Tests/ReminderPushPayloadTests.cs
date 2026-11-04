using smart_pet_care_api.Modules.NotificationModule.Domain;
using Xunit;

namespace smart_pet_care_api.Modules.NotificationModule.Tests;

/// <summary>
/// The Android block is what the Firebase SDK renders the notification from when the app is in
/// the background, so a field left at its default there is a visible bug with nothing in the
/// logs. <c>EventTimestamp</c> is the one that bites: it is a non-nullable <see cref="DateTime"/>
/// in the admin SDK, so leaving it unset does not omit <c>event_time</c>, it sends
/// 0001-01-01T00:00:00Z as the notification's <c>when</c>.
/// </summary>
public class ReminderPushPayloadTests
{
    private static readonly (string ChannelId, string Sound) Channel = ("pet-reminders-dog-v1", "dog.wav");

    private static readonly Dictionary<string, string> Data = new() { ["reminderId"] = "r1" };

    [Fact]
    public void BuildMessage_SetsEventTimestampToTheGivenTime()
    {
        var eventTime = DateTime.UtcNow;

        var message = NotificationService.BuildMessage(
            "fcm-token-aaa", "Time to feed Test!", "Morning meal", Data, Channel, eventTime);

        var notification = message.Android!.Notification!;
        Assert.Equal(eventTime, notification.EventTimestamp);
        Assert.NotEqual(default, notification.EventTimestamp);
    }

    /// <summary>
    /// The SDK reads an <see cref="DateTimeKind.Unspecified"/> timestamp as the server's local
    /// time and converts it, which moves the displayed instant by the host's offset.
    /// </summary>
    [Fact]
    public void BuildMessage_KeepsTheEventTimestampInUtc()
    {
        var message = NotificationService.BuildMessage(
            "fcm-token-aaa", "Time to feed Test!", "Morning meal", Data, Channel, DateTime.UtcNow);

        Assert.Equal(DateTimeKind.Utc, message.Android!.Notification!.EventTimestamp.Kind);
    }

    [Fact]
    public void BuildMessage_CarriesTheSpeciesChannelAndData()
    {
        var message = NotificationService.BuildMessage(
            "fcm-token-aaa", "Time to feed Test!", "Morning meal", Data, Channel, DateTime.UtcNow);

        Assert.Equal(Channel.ChannelId, message.Android!.Notification!.ChannelId);
        Assert.Equal(Channel.Sound, message.Android.Notification.Sound);
        Assert.Equal("r1", message.Data["reminderId"]);
    }
}
