namespace SmartFleet.Services;

public interface IEmailSender
{
    void Send(string toEmail, string subject, string body);
}
