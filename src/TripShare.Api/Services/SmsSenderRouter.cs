using TripShare.Application.Abstractions;

namespace TripShare.Api.Services;

public sealed class SmsSenderRouter : ISmsSender
{
    private readonly IServiceProvider _provider;
    private readonly IConfiguration _cfg;
    private readonly ILogger<SmsSenderRouter> _log;

    public SmsSenderRouter(IServiceProvider provider, IConfiguration cfg, ILogger<SmsSenderRouter> log)
    {
        _provider = provider;
        _cfg = cfg;
        _log = log;
    }

    public async Task SendAsync(string phoneNumber, string message, CancellationToken ct)
    {
        var provider = (_cfg["Sms:Provider"] ?? "TextLk").ToLowerInvariant();
        ISmsSender sender = provider switch
        {
            "devfile" => _provider.GetRequiredService<DevFileSmsSender>(),
            "acs" => _provider.GetRequiredService<AcsSmsSender>(),
            "textlk" => _provider.GetRequiredService<TextLkSmsSender>(),
            _ => _provider.GetRequiredService<TextLkSmsSender>()
        };

        try
        {
            await sender.SendAsync(phoneNumber, message, ct);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "SMS send failed via provider {Provider}", provider);
            throw;
        }
    }
}
