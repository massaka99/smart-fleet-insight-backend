using Microsoft.Extensions.Logging;
using SmartFleet.Data;
using SmartFleet.Models;

namespace SmartFleet.Services;

public class TelemetryDeadLetterSink(
    ApplicationDbContext context,
    ILogger<TelemetryDeadLetterSink> logger) : ITelemetryDeadLetterSink
{
    private readonly ApplicationDbContext _context = context;
    private readonly ILogger<TelemetryDeadLetterSink> _logger = logger;

    public async Task StoreAsync(string reason, string payloadJson, CancellationToken cancellationToken)
    {
        try
        {
            var safeReason = string.IsNullOrWhiteSpace(reason) ? "unspecified" : reason;
            var entry = new VehicleTelemetryDeadLetter
            {
                Reason = safeReason[..Math.Min(safeReason.Length, 256)],
                PayloadJson = payloadJson,
                CreatedAtUtc = DateTime.UtcNow
            };

            _context.VehicleTelemetryDeadLetters.Add(entry);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to persist telemetry dead-letter entry.");
        }
    }
}
