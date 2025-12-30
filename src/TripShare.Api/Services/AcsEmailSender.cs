using Azure;
using Azure.Communication.Email;
using TripShare.Application.Abstractions;

namespace TripShare.Api.Services;

public sealed class AcsEmailSender : IEmailSender
{
    private readonly EmailClient _client;
    private readonly IConfiguration _cfg;
    private readonly ILogger<AcsEmailSender> _log;

    public AcsEmailSender(IConfiguration cfg, ILogger<AcsEmailSender> log)
    {
        _cfg = cfg;
        _log = log;

        var connection = cfg["Email:Acs:ConnectionString"];
        if (string.IsNullOrWhiteSpace(connection))
        {
            throw new InvalidOperationException("Email:Acs:ConnectionString missing");
        }

        _client = new EmailClient(connection);
    }

    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct)
    {
        var fromEmail = _cfg["Email:Acs:FromEmail"] ?? _cfg["Email:FromEmail"];
        if (string.IsNullOrWhiteSpace(fromEmail))
        {
            throw new InvalidOperationException("Email:Acs:FromEmail missing");
        }

        var message = new EmailMessage(
            senderAddress: fromEmail,
            content: new EmailContent(subject) { Html = htmlBody },
            recipients: new EmailRecipients(new[] { new EmailAddress(toEmail) }));

        var response = await _client.SendAsync(WaitUntil.Completed, message, ct);
        if (response.Value.Status != EmailSendStatus.Succeeded)
        {
            _log.LogWarning("ACS email send status: {Status}", response.Value.Status);
        }
    }
}
