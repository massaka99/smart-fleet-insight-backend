namespace SmartFleet.Options;

public class OtpOptions
{
    public int CodeLength { get; set; } = 6;
    public int ExpiresInMinutes { get; set; } = 5;
    public string Subject { get; set; } = "Your one-time code";
    public string BodyTemplate { get; set; } = "Your one-time code is {0}. It expires in {1} minutes.";
}
