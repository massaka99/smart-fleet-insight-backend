using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartFleet.Data;
using SmartFleet.Models.Chat;

namespace SmartFleet.Services;

public class ChatService(ApplicationDbContext context, ILogger<ChatService> logger, IChatNotifier notifier) : IChatService
{
    private readonly ApplicationDbContext _context = context;
    private readonly ILogger<ChatService> _logger = logger;
    private readonly IChatNotifier _notifier = notifier;

    public async Task<(ChatThread Thread, bool Created)> GetOrCreateThreadAsync(int userId1, int userId2, CancellationToken cancellationToken)
    {
        var (participantAId, participantBId) = ChatThread.NormalizeParticipants(userId1, userId2);

        var thread = await _context.ChatThreads
            .Include(t => t.ParticipantA)
            .Include(t => t.ParticipantB)
            .FirstOrDefaultAsync(t => t.ParticipantAId == participantAId && t.ParticipantBId == participantBId, cancellationToken);

        if (thread is not null)
        {
            return (thread, false);
        }

        var participants = await _context.Users
            .Where(u => u.Id == participantAId || u.Id == participantBId)
            .ToListAsync(cancellationToken);

        if (participants.Count != 2)
        {
            throw new InvalidOperationException("Both participants must exist to create a chat thread.");
        }

        thread = new ChatThread
        {
            ParticipantAId = participantAId,
            ParticipantBId = participantBId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            ParticipantA = participants.Single(u => u.Id == participantAId),
            ParticipantB = participants.Single(u => u.Id == participantBId)
        };

        _context.ChatThreads.Add(thread);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return (thread, true);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Potential concurrency race creating chat thread between {UserA} and {UserB}. Reloading existing thread.", participantAId, participantBId);

            thread = await _context.ChatThreads
                .Include(t => t.ParticipantA)
                .Include(t => t.ParticipantB)
                .FirstOrDefaultAsync(t => t.ParticipantAId == participantAId && t.ParticipantBId == participantBId, cancellationToken)
                ?? throw new InvalidOperationException("Failed to resolve chat thread after concurrency conflict.");

            return (thread, false);
        }
    }

    public async Task<IReadOnlyCollection<ChatMessage>> GetRecentMessagesAsync(int threadId, int take, DateTime? before, CancellationToken cancellationToken)
    {
        take = Math.Clamp(take, 1, 100);

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

    public async Task<ChatMessage> SendMessageAsync(int senderId, int recipientId, string messageBody, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(messageBody))
        {
            throw new ArgumentException("Message cannot be empty.", nameof(messageBody));
        }

        var trimmedBody = messageBody.Trim();
        var (thread, _) = await GetOrCreateThreadAsync(senderId, recipientId, cancellationToken);

        if (senderId != thread.ParticipantAId && senderId != thread.ParticipantBId)
        {
            throw new InvalidOperationException("Sender is not part of the chat thread.");
        }

        if (recipientId != thread.ParticipantAId && recipientId != thread.ParticipantBId)
        {
            throw new InvalidOperationException("Recipient is not part of the chat thread.");
        }

        var message = new ChatMessage
        {
            ThreadId = thread.Id,
            SenderId = senderId,
            RecipientId = recipientId,
            Body = trimmedBody,
            SentAt = DateTime.UtcNow,
            Status = ChatMessageStatus.Sent
        };

        _context.ChatMessages.Add(message);
        thread.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        await _context.Entry(message).Reference(m => m.Sender).LoadAsync(cancellationToken);
        await _context.Entry(message).Reference(m => m.Recipient).LoadAsync(cancellationToken);

        await BroadcastAsync(() => _notifier.MessageSentAsync(message, cancellationToken));

        return message;
    }

    public async Task<bool> MarkDeliveredAsync(int messageId, int recipientId, DateTime? deliveredAt, CancellationToken cancellationToken)
    {
        var message = await _context.ChatMessages.FirstOrDefaultAsync(m => m.Id == messageId, cancellationToken);

        if (message is null || message.RecipientId != recipientId || message.Status == ChatMessageStatus.Failed)
        {
            return false;
        }

        if (message.Status == ChatMessageStatus.Delivered || message.Status == ChatMessageStatus.Read)
        {
            return true;
        }

        message.Status = ChatMessageStatus.Delivered;
        message.DeliveredAt = deliveredAt ?? DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        await BroadcastAsync(() => _notifier.MessageDeliveredAsync(message, cancellationToken));
        return true;
    }

    public async Task<bool> MarkReadAsync(int messageId, int recipientId, DateTime? readAt, CancellationToken cancellationToken)
    {
        var message = await _context.ChatMessages.FirstOrDefaultAsync(m => m.Id == messageId, cancellationToken);

        if (message is null || message.RecipientId != recipientId || message.Status == ChatMessageStatus.Failed)
        {
            return false;
        }

        if (message.Status == ChatMessageStatus.Read)
        {
            return true;
        }

        message.Status = ChatMessageStatus.Read;
        message.ReadAt = readAt ?? DateTime.UtcNow;

        if (message.DeliveredAt is null)
        {
            message.DeliveredAt = message.ReadAt;
        }

        await _context.SaveChangesAsync(cancellationToken);
        await BroadcastAsync(() => _notifier.MessageReadAsync(message, cancellationToken));
        return true;
    }

    public async Task UpdateMessageStatusesAsync(
        int recipientId,
        IReadOnlyCollection<int> deliveredMessageIds,
        IReadOnlyCollection<int> readMessageIds,
        DateTime? deliveredAt,
        DateTime? readAt,
        CancellationToken cancellationToken)
    {
        var deliveredSet = deliveredMessageIds?
            .Where(id => id > 0)
            .ToHashSet() ?? new HashSet<int>();
        var readSet = readMessageIds?
            .Where(id => id > 0)
            .ToHashSet() ?? new HashSet<int>();

        if (deliveredSet.Count == 0 && readSet.Count == 0)
        {
            return;
        }

        var targetIds = new HashSet<int>(deliveredSet);
        targetIds.UnionWith(readSet);

        var messages = await _context.ChatMessages
            .Where(m => targetIds.Contains(m.Id))
            .ToListAsync(cancellationToken);

        if (messages.Count == 0)
        {
            return;
        }

        var deliveredNotifications = new List<ChatMessage>();
        var readNotifications = new List<ChatMessage>();
        var now = DateTime.UtcNow;
        var effectiveDeliveredAt = deliveredAt ?? now;
        var effectiveReadAt = readAt ?? now;
        var updated = false;

        foreach (var message in messages)
        {
            if (message.RecipientId != recipientId || message.Status == ChatMessageStatus.Failed)
            {
                continue;
            }

            var isReadTarget = readSet.Contains(message.Id);
            var isDeliveredTarget = !isReadTarget && deliveredSet.Contains(message.Id);
            var changed = false;

            if (isReadTarget)
            {
                if (message.Status != ChatMessageStatus.Read)
                {
                    message.Status = ChatMessageStatus.Read;
                    changed = true;
                }

                if (message.ReadAt is null || message.ReadAt < effectiveReadAt)
                {
                    message.ReadAt = effectiveReadAt;
                    changed = true;
                }

                if (message.DeliveredAt is null || message.DeliveredAt < effectiveDeliveredAt)
                {
                    message.DeliveredAt = effectiveDeliveredAt;
                    changed = true;
                }

                if (changed)
                {
                    readNotifications.Add(message);
                }
            }
            else if (isDeliveredTarget)
            {
                if (message.Status == ChatMessageStatus.Sent)
                {
                    message.Status = ChatMessageStatus.Delivered;
                    changed = true;
                }

                if (message.DeliveredAt is null || message.DeliveredAt < effectiveDeliveredAt)
                {
                    message.DeliveredAt = effectiveDeliveredAt;
                    changed = true;
                }

                if (changed)
                {
                    deliveredNotifications.Add(message);
                }
            }

            if (changed)
            {
                updated = true;
            }
        }

        if (!updated)
        {
            return;
        }

        await _context.SaveChangesAsync(cancellationToken);

        foreach (var message in deliveredNotifications)
        {
            await BroadcastAsync(() => _notifier.MessageDeliveredAsync(message, cancellationToken));
        }

        foreach (var message in readNotifications)
        {
            await BroadcastAsync(() => _notifier.MessageReadAsync(message, cancellationToken));
        }
    }

    private async Task BroadcastAsync(Func<Task> notifierAction)
    {
        try
        {
            await notifierAction();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast chat notification");
        }
    }
}
