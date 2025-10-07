using System.ComponentModel.DataAnnotations;
using SmartFleet.Models;

namespace SmartFleet.Models.Chat;

public class ChatMessage
{
    public int Id { get; set; }
    public int ThreadId { get; set; }
    public int SenderId { get; set; }
    public int RecipientId { get; set; }

    [Required]
    [StringLength(4000)]
    public string Body { get; set; } = string.Empty;

    public ChatMessageStatus Status { get; set; } = ChatMessageStatus.Sent;

    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeliveredAt { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime? FailedAt { get; set; }

    [StringLength(1024)]
    public string? FailureReason { get; set; }

    public ChatThread Thread { get; set; } = null!;
    public User Sender { get; set; } = null!;
    public User Recipient { get; set; } = null!;
}
