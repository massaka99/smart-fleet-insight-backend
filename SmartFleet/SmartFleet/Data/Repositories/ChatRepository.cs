using Microsoft.EntityFrameworkCore;
using SmartFleet.Models.Chat;

namespace SmartFleet.Data.Repositories;

public class ChatRepository(ApplicationDbContext context) : IChatRepository
{
    private readonly ApplicationDbContext _context = context;

    public async Task<ChatThread?> GetThreadAsync(int participantAId, int participantBId, CancellationToken cancellationToken)
    {
        return await _context.ChatThreads
            .Include(t => t.ParticipantA)
            .Include(t => t.ParticipantB)
            .FirstOrDefaultAsync(t => t.ParticipantAId == participantAId && t.ParticipantBId == participantBId, cancellationToken);
    }

    public async Task AddThreadAsync(ChatThread thread, CancellationToken cancellationToken)
    {
        await _context.ChatThreads.AddAsync(thread, cancellationToken);
    }

    public async Task<IReadOnlyCollection<ChatMessage>> GetRecentMessagesAsync(int threadId, int take, DateTime? before, CancellationToken cancellationToken)
    {
        var query = _context.ChatMessages
            .Where(m => m.ThreadId == threadId);

        if (before.HasValue)
        {
            query = query.Where(m => m.SentAt < before.Value);
        }

        return await query
            .OrderByDescending(m => m.SentAt)
            .Include(m => m.Sender)
            .Include(m => m.Recipient)
            .Take(take)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task AddMessageAsync(ChatMessage message, CancellationToken cancellationToken)
    {
        await _context.ChatMessages.AddAsync(message, cancellationToken);
    }

    public async Task<ChatMessage?> GetMessageByIdAsync(int messageId, CancellationToken cancellationToken)
    {
        return await _context.ChatMessages.FirstOrDefaultAsync(m => m.Id == messageId, cancellationToken);
    }

    public async Task<IReadOnlyCollection<ChatMessage>> GetMessagesByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return Array.Empty<ChatMessage>();
        }

        return await _context.ChatMessages
            .Where(m => ids.Contains(m.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task RemoveThreadsByParticipantAsync(int userId, CancellationToken cancellationToken)
    {
        var threads = await _context.ChatThreads
            .Where(t => t.ParticipantAId == userId || t.ParticipantBId == userId)
            .ToListAsync(cancellationToken);

        if (threads.Count > 0)
        {
            _context.ChatThreads.RemoveRange(threads);
        }
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => _context.SaveChangesAsync(cancellationToken);
}
