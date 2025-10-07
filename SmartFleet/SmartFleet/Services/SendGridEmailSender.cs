using Microsoft.Extensions.Options;
using SendGrid;
using SendGrid.Helpers.Mail;
using SmartFleet.Options;

namespace SmartFleet.Services;

public class SendGridEmailSender : IEmailSender
{
    private readonly SendGridOptions _options;
    private readonly SendGridClient _client;

    public SendGridEmailSender(IOptions<SendGridOptions> options)
    {
        _options = options.Value;
        _client = new SendGridClient(_options.ApiKey);
    }

    public async Task SendAsync(string toEmail, string templateId, object templateData, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.FromEmail))
        {
            throw new InvalidOperationException("SendGrid FromEmail must be configured.");
        }

        var message = new SendGridMessage
        {
            TemplateId = templateId,
            From = new EmailAddress(_options.FromEmail, string.IsNullOrWhiteSpace(_options.FromName) ? null : _options.FromName)
        };

        message.AddTo(new EmailAddress(toEmail));
        message.SetTemplateData(templateData);

        var response = await _client.SendEmailAsync(message, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Body.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Failed to send email via SendGrid. Status: {response.StatusCode}. Body: {body}");
        }
    }
}
