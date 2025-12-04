using System.IdentityModel.Tokens.Jwt;
using System.Collections.Generic;
using System.Security.Claims;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartFleet.Dtos;
using SmartFleet.Models;
using SmartFleet.Services;

namespace SmartFleet.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ChatController(
    IChatService chatService,
    IUserService userService,
    ILogger<ChatController> logger) : ControllerBase
{
    private readonly IChatService _chatService = chatService;
    private readonly IUserService _userService = userService;
    private readonly ILogger<ChatController> _logger = logger;

    [HttpGet("{recipientId:int}/thread")]
    public async Task<ActionResult<ChatThreadDto>> GetThread(int recipientId, [FromQuery] int take = 50, [FromQuery] DateTime? before = null, CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var requesterId))
        {
            return Unauthorized();
        }

        if (recipientId == requesterId)
        {
            return BadRequest(new { message = "Cannot start a chat with yourself." });
        }

        var requester = await _userService.GetByIdAsync(requesterId, cancellationToken);

        if (requester is null)
        {
            return Unauthorized();
        }

        try
        {
            var (thread, _) = await _chatService.GetOrCreateThreadAsync(requesterId, recipientId, cancellationToken);
            var messages = await _chatService.GetRecentMessagesAsync(thread.Id, take, before, cancellationToken);
            return Ok(thread.ToChatThreadDto(requester, messages));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Unable to open chat between {RequesterId} and {RecipientId}", requesterId, recipientId);
            return NotFound();
        }
    }

    [HttpGet("participants")]
    public async Task<ActionResult<IEnumerable<ChatParticipantDto>>> GetParticipants(CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var requesterId))
        {
            return Unauthorized();
        }

        var participants = await _userService.GetAllAsync(cancellationToken);

        var participantDtos = participants
            .Where(u => u.Id != requesterId)
            .OrderBy(u => u.FirstName)
            .ThenBy(u => u.LastName)
            .Select(u => u.ToChatParticipantDto())
            .ToList();

        return Ok(participantDtos);
    }

    [HttpPost("{recipientId:int}/messages")]
    public async Task<ActionResult<ChatMessageDto>> SendMessage(int recipientId, [FromBody] SendChatMessageRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var senderId))
        {
            return Unauthorized();
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var message = await _chatService.SendMessageAsync(senderId, recipientId, request.Message, cancellationToken);
            return CreatedAtAction(nameof(GetThread), new { recipientId }, message.ToChatMessageDto());
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(nameof(request.Message), ex.Message);
            return ValidationProblem(ModelState);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Chat message validation failed for sender {SenderId} to recipient {RecipientId}", senderId, recipientId);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send chat message from {SenderId} to {RecipientId}", senderId, recipientId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Kunne ikke sende beskeden. Pr�v igen." });
        }
    }

    [HttpPost("messages/{messageId:int}/status")]
    public async Task<IActionResult> UpdateMessageStatus(int messageId, [FromBody] ChatMessageDeliveryUpdateDto request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var updated = false;

        if (request.DeliveredAt.HasValue)
        {
            updated |= await _chatService.MarkDeliveredAsync(messageId, userId, request.DeliveredAt, cancellationToken);
        }

        if (request.ReadAt.HasValue)
        {
            updated |= await _chatService.MarkReadAsync(messageId, userId, request.ReadAt, cancellationToken);
        }

        return updated ? NoContent() : NotFound();
    }

    [HttpPost("messages/status")]
    public async Task<IActionResult> UpdateMessageStatuses([FromBody] ChatMessageStatusBatchUpdateDto request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var deliveredIds = request.DeliveredMessageIds ?? Array.Empty<int>();
        var readIds = request.ReadMessageIds ?? Array.Empty<int>();

        if (deliveredIds.Count == 0 && readIds.Count == 0)
        {
            return NoContent();
        }

        await _chatService.UpdateMessageStatusesAsync(
            userId,
            deliveredIds,
            readIds,
            request.DeliveredAt,
            request.ReadAt,
            cancellationToken);

        return NoContent();
    }

    private bool TryGetUserId(out int userId)
    {
        var claim = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(claim, out userId);
    }
}



