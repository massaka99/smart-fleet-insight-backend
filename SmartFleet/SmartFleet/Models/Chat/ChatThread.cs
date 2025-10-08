using System.ComponentModel.DataAnnotations.Schema;
using SmartFleet.Models;

namespace SmartFleet.Models.Chat;

public class ChatThread
{
    public int Id { get; set; }
    public int ParticipantAId { get; set; }
    public int ParticipantBId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User ParticipantA { get; set; } = null!;
    public User ParticipantB { get; set; } = null!;
    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();

    [NotMapped]
    public string ThreadKey => GenerateThreadKey(ParticipantAId, ParticipantBId);

    public static (int ParticipantAId, int ParticipantBId) NormalizeParticipants(int userId1, int userId2)
    {
        if (userId1 == userId2)
        {
            throw new ArgumentException("Chat thread requires two distinct participants.");
        }

        return userId1 < userId2 ? (userId1, userId2) : (userId2, userId1);
    }

    public static string GenerateThreadKey(int userId1, int userId2)
    {
        var (a, b) = NormalizeParticipants(userId1, userId2);
        return $"{a}:{b}";
    }
}
