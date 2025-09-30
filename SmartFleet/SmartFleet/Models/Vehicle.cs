namespace SmartFleet.Models;

public class Vehicle
{
    public int Id { get; set; }
    public DateTime CreatedUtc { get; set; }
    public string RegistrationPlate { get; set; } = string.Empty;
    public string VehicleType { get; set; } = string.Empty;
    public string FuelType { get; set; } = string.Empty;
    public string BodyType { get; set; } = string.Empty;
    public int KilometersDriven { get; set; }
    public double Co2Emission { get; set; }
}
