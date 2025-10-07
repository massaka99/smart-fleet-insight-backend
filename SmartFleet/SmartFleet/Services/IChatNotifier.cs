using SmartFleet.Models.Chat;

namespace SmartFleet.Services;

public interface IChatNotifier
{
    Task MessageSentAsync(ChatMessage message, CancellationToken cancellationToken);
    Task MessageDeliveredAsync(ChatMessage message, CancellationToken cancellationToken);
    Task MessageReadAsync(ChatMessage message, CancellationToken cancellationToken);
}
