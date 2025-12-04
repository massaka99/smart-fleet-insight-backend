using SmartFleet.Models.Chat;

namespace SmartFleet.Data.Repositories;

public interface IChatRepository
{
    Task<ChatThread?> GetThreadAsync(int participantAId, int participantBId, CancellationToken cancellationToken);
    Task AddThreadAsync(ChatThread thread, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ChatMessage>> GetRecentMessagesAsync(int threadId, int take, DateTime? before, CancellationToken cancellationToken);
    Task AddMessageAsync(ChatMessage message, CancellationToken cancellationToken);
    Task<ChatMessage?> GetMessageByIdAsync(int messageId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ChatMessage>> GetMessagesByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken);
    Task RemoveThreadsByParticipantAsync(int userId, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
