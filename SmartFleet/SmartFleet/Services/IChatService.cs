using SmartFleet.Models.Chat;

namespace SmartFleet.Services;

public interface IChatService
{
    Task<(ChatThread Thread, bool Created)> GetOrCreateThreadAsync(int userId1, int userId2, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ChatMessage>> GetRecentMessagesAsync(int threadId, int take, DateTime? before, CancellationToken cancellationToken);
    Task<ChatMessage> SendMessageAsync(int senderId, int recipientId, string messageBody, CancellationToken cancellationToken);
    Task<bool> MarkDeliveredAsync(int messageId, int recipientId, DateTime? deliveredAt, CancellationToken cancellationToken);
    Task<bool> MarkReadAsync(int messageId, int recipientId, DateTime? readAt, CancellationToken cancellationToken);
}
