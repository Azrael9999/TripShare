using Azure;
using Azure.Communication.Sms;
using TripShare.Application.Abstractions;

namespace TripShare.Api.Services;

public sealed class AcsSmsSender : ISmsSender
{
    private readonly SmsClient _client;
    private readonly IConfiguration _cfg;
    private readonly ILogger<AcsSmsSender> _log;

    public AcsSmsSender(IConfiguration cfg, ILogger<AcsSmsSender> log)
    {
        _cfg = cfg;
        _log = log;

        var connection = cfg["Sms:Acs:ConnectionString"];
        if (string.IsNullOrWhiteSpace(connection))
        {
            throw new InvalidOperationException("Sms:Acs:ConnectionString missing");
        }

        _client = new SmsClient(connection);
    }

    public async Task SendAsync(string phoneNumber, string message, CancellationToken ct)
    {
        var sender = _cfg["Sms:Acs:Sender"];
        if (string.IsNullOrWhiteSpace(sender))
        {
            throw new InvalidOperationException("Sms:Acs:Sender missing");
        }

        var response = await _client.SendAsync(WaitUntil.Completed, from: sender, to: phoneNumber, message: message, cancellationToken: ct);
        if (!response.Value.Successful)
        {
            _log.LogError("ACS SMS send failed. Status: {Status} Error: {Message}", response.Value.HttpStatusCode, response.Value.ErrorMessage);
            throw new InvalidOperationException("Failed to send SMS via ACS.");
        }
    }
}
