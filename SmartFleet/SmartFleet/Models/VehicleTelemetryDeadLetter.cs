using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartFleet.Models;

public class VehicleTelemetryDeadLetter
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [StringLength(256)]
    public string Reason { get; set; } = string.Empty;

    [Column(TypeName = "jsonb")]
    public string PayloadJson { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
