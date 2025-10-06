namespace SmartFleet.Services;

public interface IEmailSender
{
    Task SendAsync(string toEmail, string templateId, object templateData, CancellationToken cancellationToken);
}
