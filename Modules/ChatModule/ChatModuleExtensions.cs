using smart_pet_care_api.Modules.ChatModule.Domain;
using smart_pet_care_api.Modules.ChatModule.Recovery;

namespace smart_pet_care_api.Modules.ChatModule;

public static class ChatModuleExtensions
{
    public static IServiceCollection AddChatModule(this IServiceCollection services)
    {
        services.AddScoped<IChatService, ChatService>();
        services.AddScoped<ChatPendingMessageRecovery>();
        services.AddHostedService<ChatPendingMessageRecoveryService>();
        return services;
    }
}
