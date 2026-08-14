using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using smart_pet_care_api.Data;
using smart_pet_care_api.Infrastructure.Classifier;
using smart_pet_care_api.Models;

namespace smart_pet_care_api.Modules.ChatModule.Recovery;

public sealed class ChatPendingMessageRecovery(
    AppDbContext dbContext,
    IOptions<ClassifierOptions> classifierOptions,
    TimeProvider timeProvider)
{
    public async Task<int> RecoverStalePendingMessagesAsync(
        CancellationToken cancellationToken = default)
    {
        var cutoff = timeProvider.GetUtcNow().UtcDateTime.AddSeconds(
            -classifierOptions.Value.TimeoutSeconds);

        var staleMessages = dbContext.ChatMessages.Where(message =>
            message.Role == ChatMessageRole.User
            && message.Status == ChatMessageStatus.Pending
            && message.CreatedAt < cutoff);

        if (dbContext.Database.IsRelational())
        {
            return await staleMessages.ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    message => message.Status,
                    ChatMessageStatus.FailedRetryable),
                cancellationToken);
        }

        var messages = await staleMessages.ToListAsync(cancellationToken);
        foreach (var message in messages)
        {
            message.Status = ChatMessageStatus.FailedRetryable;
        }

        if (messages.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return messages.Count;
    }
}
