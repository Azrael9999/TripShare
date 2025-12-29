using TripShare.Application.Abstractions;

namespace TripShare.Api.Services;

public sealed class DevFileEmailSender : IEmailSender
{
    private readonly IConfiguration _cfg;
    private readonly ILogger _log;

    public DevFileEmailSender(IConfiguration cfg, ILogger log)
    {
        _cfg = cfg;
        _log = log;
    }

    public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct)
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "App_Data", "dev-emails");
        Directory.CreateDirectory(dir);

        var safe = string.Join("_", toEmail.Split(Path.GetInvalidFileNameChars())).Replace("@", "_at_");
        var path = Path.Combine(dir, $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{safe}.html");
        File.WriteAllText(path, $"<h3>{subject}</h3>\n{htmlBody}");

        _log.LogInformation("DEV email written to {Path} for {To}", path, toEmail);
        return Task.CompletedTask;
    }
}
