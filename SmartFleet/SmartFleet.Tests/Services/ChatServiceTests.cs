using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SmartFleet.Data;
using SmartFleet.Data.Repositories;
using SmartFleet.Models;
using SmartFleet.Models.Chat;
using SmartFleet.Services;

namespace SmartFleet.Tests.Services;

public class ChatServiceTests
{
    [Fact]
    public async Task SendMessageAsync_CreatesThreadAndPersistsAndNotifies()
    {
        await using var context = CreateContext();
        var userRepo = new UserRepository(context);
        var chatRepo = new ChatRepository(context);
        var notifier = new Mock<IChatNotifier>();
        var service = new ChatService(chatRepo, userRepo, NullLogger<ChatService>.Instance, notifier.Object);

        var sender = new User { Id = 1, FirstName = "A", LastName = "A", Email = "a@example.com" };
        var recipient = new User { Id = 2, FirstName = "B", LastName = "B", Email = "b@example.com" };
        context.Users.AddRange(sender, recipient);
        await context.SaveChangesAsync();

        var message = await service.SendMessageAsync(sender.Id, recipient.Id, " Hello ", CancellationToken.None);

        message.Body.Should().Be("Hello");
        message.Status.Should().Be(ChatMessageStatus.Sent);
        (await context.ChatThreads.CountAsync()).Should().Be(1);
        (await context.ChatMessages.CountAsync()).Should().Be(1);
        notifier.Verify(n => n.MessageSentAsync(It.IsAny<ChatMessage>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MarkDeliveredAsync_ReturnsFalse_ForUnknownRecipient()
    {
        await using var context = CreateContext();
        var userRepo = new UserRepository(context);
        var chatRepo = new ChatRepository(context);
        var service = new ChatService(chatRepo, userRepo, NullLogger<ChatService>.Instance, Mock.Of<IChatNotifier>());

        var sender = new User { Id = 3, FirstName = "A", LastName = "A", Email = "a3@example.com" };
        var recipient = new User { Id = 4, FirstName = "B", LastName = "B", Email = "b4@example.com" };
        context.Users.AddRange(sender, recipient);
        var thread = new ChatThread { ParticipantAId = sender.Id, ParticipantBId = recipient.Id };
        context.ChatThreads.Add(thread);
        var msg = new ChatMessage { Thread = thread, ThreadId = thread.Id, SenderId = sender.Id, RecipientId = recipient.Id, Body = "msg", Status = ChatMessageStatus.Sent };
        context.ChatMessages.Add(msg);
        await context.SaveChangesAsync();

        var result = await service.MarkDeliveredAsync(msg.Id, recipientId: 999, deliveredAt: DateTime.UtcNow, CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task MarkReadAsync_SetsStatusAndNotifies_WhenValid()
    {
        await using var context = CreateContext();
        var userRepo = new UserRepository(context);
        var chatRepo = new ChatRepository(context);
        var notifier = new Mock<IChatNotifier>();
        var service = new ChatService(chatRepo, userRepo, NullLogger<ChatService>.Instance, notifier.Object);

        var sender = new User { Id = 10, FirstName = "A", LastName = "A", Email = "a10@example.com" };
        var recipient = new User { Id = 11, FirstName = "B", LastName = "B", Email = "b11@example.com" };
        context.Users.AddRange(sender, recipient);
        var thread = new ChatThread { ParticipantAId = sender.Id, ParticipantBId = recipient.Id };
        context.ChatThreads.Add(thread);
        var msg = new ChatMessage { Thread = thread, ThreadId = thread.Id, SenderId = sender.Id, RecipientId = recipient.Id, Body = "msg", Status = ChatMessageStatus.Sent };
        context.ChatMessages.Add(msg);
        await context.SaveChangesAsync();

        var ok = await service.MarkReadAsync(msg.Id, recipient.Id, DateTime.UtcNow, CancellationToken.None);

        ok.Should().BeTrue();
        (await context.ChatMessages.AsNoTracking().SingleAsync(m => m.Id == msg.Id)).Status.Should().Be(ChatMessageStatus.Read);
        notifier.Verify(n => n.MessageReadAsync(It.IsAny<ChatMessage>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }
}
