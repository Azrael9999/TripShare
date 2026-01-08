using System.Text;
using TripShare.Application.Abstractions;

namespace TripShare.Api.Services;

public sealed class DevFileSmsSender : ISmsSender
{
    private readonly ILogger<DevFileSmsSender> _log;
    private readonly IConfiguration _cfg;

    public DevFileSmsSender(IConfiguration cfg, ILogger<DevFileSmsSender> log)
    {
        _cfg = cfg;
        _log = log;
    }

    public async Task SendAsync(string phoneNumber, string message, CancellationToken ct)
    {
        var dir = _cfg["Sms:DevFileOutputDir"];
        if (string.IsNullOrWhiteSpace(dir))
        {
            dir = Path.Combine(AppContext.BaseDirectory, "App_Data", "dev-sms");
        }

        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, $"{DateTimeOffset.UtcNow:yyyyMMdd-HHmmssfff}-{Sanitize(phoneNumber)}.txt");
        var content = new StringBuilder()
            .AppendLine($"To: {phoneNumber}")
            .AppendLine($"Sent: {DateTimeOffset.UtcNow:O}")
            .AppendLine()
            .AppendLine(message)
            .ToString();

        await File.WriteAllTextAsync(file, content, ct);
        _log.LogInformation("Dev SMS written to {File}", file);
    }

    private static string Sanitize(string value)
        => string.Concat(value.Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_'));
}
