using Microsoft.AspNetCore.SignalR;
using SmartFleet.Dtos;
using SmartFleet.Hubs;
using SmartFleet.Models.Chat;

namespace SmartFleet.Services;

public class SignalRChatNotifier(IHubContext<ChatHub> hubContext) : IChatNotifier
{
    private readonly IHubContext<ChatHub> _hubContext = hubContext;

    public Task MessageSentAsync(ChatMessage message, CancellationToken cancellationToken)
    {
        var payload = message.ToChatMessageDto();
        return BroadcastAsync("ReceiveMessage", payload, message, cancellationToken);
    }

    public Task MessageDeliveredAsync(ChatMessage message, CancellationToken cancellationToken)
    {
        var payload = new
        {
            messageId = message.Id,
            threadId = message.ThreadId,
            deliveredAt = message.DeliveredAt
        };

        return BroadcastAsync("MessageDelivered", payload, message, cancellationToken);
    }

    public Task MessageReadAsync(ChatMessage message, CancellationToken cancellationToken)
    {
        var payload = new
        {
            messageId = message.Id,
            threadId = message.ThreadId,
            readAt = message.ReadAt
        };

        return BroadcastAsync("MessageRead", payload, message, cancellationToken);
    }

    private Task BroadcastAsync(string method, object payload, ChatMessage message, CancellationToken cancellationToken)
    {
        var userIds = new[] { message.SenderId.ToString(), message.RecipientId.ToString() };
        return _hubContext.Clients.Users(userIds).SendAsync(method, payload, cancellationToken);
    }
}
