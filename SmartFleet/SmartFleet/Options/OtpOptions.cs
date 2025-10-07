namespace SmartFleet.Options;

public class OtpOptions
{
    public int CodeLength { get; set; } = 6;
    public int ExpiresInMinutes { get; set; } = 5;
}
