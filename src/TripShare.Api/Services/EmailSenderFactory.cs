using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TripShare.Application.Abstractions;

namespace TripShare.Api.Services;

public sealed class EmailSenderFactory : IEmailSender
{
    private readonly IConfiguration _cfg;
    private readonly ILogger<EmailSenderFactory> _log;
    private readonly IServiceProvider _services;

    public EmailSenderFactory(IConfiguration cfg, ILogger<EmailSenderFactory> log, IServiceProvider services)
    {
        _cfg = cfg;
        _log = log;
        _services = services;
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
            var sender = ActivatorUtilities.CreateInstance<AcsEmailSender>(_services);
            await sender.SendAsync(toEmail, subject, htmlBody, ct);
            return;
        }

        var dev = new DevFileEmailSender(_cfg, _log);
        await dev.SendAsync(toEmail, subject, htmlBody, ct);
    }
}
