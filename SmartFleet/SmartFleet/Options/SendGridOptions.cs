namespace SmartFleet.Options;

public class SendGridOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;
    public string OtpTemplateId { get; set; } = string.Empty;
}
