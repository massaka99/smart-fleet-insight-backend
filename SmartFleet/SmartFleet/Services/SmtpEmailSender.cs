using System.IO;
using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartFleet.Options;

namespace SmartFleet.Services;

public class SmtpEmailSender : IEmailSender
{
    private readonly SmtpOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public void Send(string toEmail, string subject, string body)
    {
        if (string.IsNullOrWhiteSpace(_options.FromEmail))
        {
            throw new InvalidOperationException("SMTP FromEmail must be configured.");
        }

        using var message = new MailMessage
        {
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };

        message.From = string.IsNullOrWhiteSpace(_options.FromName)
            ? new MailAddress(_options.FromEmail)
            : new MailAddress(_options.FromEmail, _options.FromName);

        message.To.Add(new MailAddress(toEmail));

        try
        {
            using var client = CreateClient();
            client.Send(message);
        }
        catch (SmtpException ex)
        {
            _logger.LogError(ex, "Failed to send email via SMTP Host={Host} Port={Port}", _options.Host, _options.Port);
            throw;
        }
    }

    private SmtpClient CreateClient()
    {
        if (_options.UsePickupDirectory)
        {
            if (string.IsNullOrWhiteSpace(_options.PickupDirectoryLocation))
            {
                throw new InvalidOperationException("PickupDirectoryLocation must be configured when UsePickupDirectory is enabled.");
            }

            var absolutePath = Path.IsPathRooted(_options.PickupDirectoryLocation)
                ? _options.PickupDirectoryLocation
                : Path.Combine(AppContext.BaseDirectory, _options.PickupDirectoryLocation);

            Directory.CreateDirectory(absolutePath);

            return new SmtpClient
            {
                DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory,
                PickupDirectoryLocation = absolutePath
            };
        }

        var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = _options.UseDefaultCredentials
        };

        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            client.Credentials = new NetworkCredential(_options.Username, _options.Password);
        }

        return client;
    }
}
