using Microsoft.Extensions.Options;
using TripShare.Application.Abstractions;

namespace TripShare.Api.Services;

public sealed class EmailSenderFactory : IEmailSender
{
    private readonly IConfiguration _cfg;
    private readonly ILogger<EmailSenderFactory> _log;

    public EmailSenderFactory(IConfiguration cfg, ILogger<EmailSenderFactory> log)
    {
        _cfg = cfg;
        _log = log;
    }

    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct)
    {
        var mode = _cfg["Email:Mode"] ?? "DevFile";
        if (mode.Equals("Smtp", StringComparison.OrdinalIgnoreCase))
        {
            var sender = new SmtpEmailSender(_cfg, _log);
            await sender.SendAsync(toEmail, subject, htmlBody, ct);
            return;
        }

        if (mode.Equals("Acs", StringComparison.OrdinalIgnoreCase))
        {
            var sender = new AcsEmailSender(_cfg, _log);
            await sender.SendAsync(toEmail, subject, htmlBody, ct);
            return;
        }

        var dev = new DevFileEmailSender(_cfg, _log);
        await dev.SendAsync(toEmail, subject, htmlBody, ct);
    }
}
