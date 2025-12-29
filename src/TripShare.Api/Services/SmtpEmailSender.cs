using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using TripShare.Application.Abstractions;

namespace TripShare.Api.Services;

public sealed class SmtpEmailSender : IEmailSender
{
    private readonly IConfiguration _cfg;
    private readonly ILogger _log;

    public SmtpEmailSender(IConfiguration cfg, ILogger log)
    {
        _cfg = cfg;
        _log = log;
    }

    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct)
    {
        var fromName = _cfg["Email:FromName"] ?? "TripShare";
        var fromEmail = _cfg["Email:FromEmail"] ?? "no-reply@tripshare.lk";

        var host = _cfg["Email:Smtp:Host"] ?? "";
        var port = int.TryParse(_cfg["Email:Smtp:Port"], out var p) ? p : 587;
        var useSsl = bool.TryParse(_cfg["Email:Smtp:UseSsl"], out var s) ? s : true;
        var username = _cfg["Email:Smtp:Username"] ?? "";
        var password = _cfg["Email:Smtp:Password"] ?? "";

        if (string.IsNullOrWhiteSpace(host))
            throw new InvalidOperationException("SMTP host not configured.");

        var msg = new MimeMessage();
        msg.From.Add(new MailboxAddress(fromName, fromEmail));
        msg.To.Add(MailboxAddress.Parse(toEmail));
        msg.Subject = subject;
        msg.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(host, port, useSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable, ct);
        if (!string.IsNullOrWhiteSpace(username))
            await client.AuthenticateAsync(username, password, ct);
        await client.SendAsync(msg, ct);
        await client.DisconnectAsync(true, ct);

        _log.LogInformation("SMTP email sent to {To}", toEmail);
    }
}
