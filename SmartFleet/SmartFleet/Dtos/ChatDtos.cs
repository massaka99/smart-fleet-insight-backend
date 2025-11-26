using System.ComponentModel.DataAnnotations;
using System.Linq;
using SmartFleet.Models;
using SmartFleet.Models.Chat;

namespace SmartFleet.Dtos;

public class ChatThreadDto
{
    public int Id { get; init; }
    public ChatParticipantDto Participant { get; init; } = null!;
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public IReadOnlyCollection<ChatMessageDto> Messages { get; init; } = Array.Empty<ChatMessageDto>();
}

public class ChatParticipantDto
{
    public int Id { get; init; }
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? ProfileImageUrl { get; init; }
}

public class ChatMessageDto
{
    public int Id { get; init; }
    public int ThreadId { get; init; }
    public int SenderId { get; init; }
    public int RecipientId { get; init; }
    public ChatParticipantDto? Sender { get; init; }
    public ChatParticipantDto? Recipient { get; init; }
    public string Body { get; init; } = string.Empty;
    public ChatMessageStatus Status { get; init; }
    public DateTime SentAt { get; init; }
    public DateTime? DeliveredAt { get; init; }
    public DateTime? ReadAt { get; init; }
}

public class SendChatMessageRequest
{
    [Required]
    [StringLength(4000)]
    public string Message { get; init; } = string.Empty;
}

public class ChatMessageDeliveryUpdateDto
{
    public DateTime? DeliveredAt { get; init; }
    public DateTime? ReadAt { get; init; }
}

public class ChatMessageStatusBatchUpdateDto
{
    public IReadOnlyCollection<int> DeliveredMessageIds { get; init; } = Array.Empty<int>();
    public IReadOnlyCollection<int> ReadMessageIds { get; init; } = Array.Empty<int>();
    public DateTime? DeliveredAt { get; init; }
    public DateTime? ReadAt { get; init; }
}

public static class ChatMappingExtensions
{
    public static ChatThreadDto ToChatThreadDto(this ChatThread thread, User requester, IEnumerable<ChatMessage> messages) => new()
    {
        Id = thread.Id,
        Participant = GetOtherParticipant(thread, requester).ToChatParticipantDto(),
        CreatedAt = thread.CreatedAt,
        UpdatedAt = thread.UpdatedAt,
        Messages = messages
            .OrderBy(m => m.SentAt)
            .Select(m => m.ToChatMessageDto())
            .ToArray()
    };

    public static ChatMessageDto ToChatMessageDto(this ChatMessage message) => new()
    {
        Id = message.Id,
        ThreadId = message.ThreadId,
        SenderId = message.SenderId,
        RecipientId = message.RecipientId,
        Sender = message.Sender?.ToChatParticipantDto(),
        Recipient = message.Recipient?.ToChatParticipantDto(),
        Body = message.Body,
        Status = message.Status,
        SentAt = message.SentAt,
        DeliveredAt = message.DeliveredAt,
        ReadAt = message.ReadAt
    };

    public static ChatParticipantDto ToChatParticipantDto(this User user) => new()
    {
        Id = user.Id,
        FirstName = user.FirstName,
        LastName = user.LastName,
        Email = user.Email,
        ProfileImageUrl = ProfileImageSanitizer.Normalize(user.ProfileImageUrl)
    };

    private static User GetOtherParticipant(ChatThread thread, User requester)
    {
        if (requester.Id == thread.ParticipantAId)
        {
            return thread.ParticipantB;
        }

        if (requester.Id == thread.ParticipantBId)
        {
            return thread.ParticipantA;
        }

        throw new InvalidOperationException("Requester is not part of the chat thread.");
    }
}
