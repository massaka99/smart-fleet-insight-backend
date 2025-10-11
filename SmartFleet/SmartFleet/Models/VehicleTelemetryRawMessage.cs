using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartFleet.Models;

public class VehicleTelemetryRawMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [StringLength(36)]
    public string? TelemetryId { get; set; }

    [StringLength(32)]
    public string? VehicleCode { get; set; }

    [Required]
    public DateTime ReceivedAtUtc { get; set; } = DateTime.UtcNow;

    [Column(TypeName = "jsonb")]
    [Required]
    public string PayloadJson { get; set; } = string.Empty;
}
