using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using TripShare.Application.Abstractions;

namespace TripShare.Api.Services;

internal sealed class TextLkSmsSender : ISmsSender
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _cfg;
    private readonly ILogger<TextLkSmsSender> _log;

    public TextLkSmsSender(IHttpClientFactory httpClientFactory, IConfiguration cfg, ILogger<TextLkSmsSender> log)
    {
        _httpClientFactory = httpClientFactory;
        _cfg = cfg;
        _log = log;
    }

    public async Task SendAsync(string phoneNumber, string message, CancellationToken ct)
    {
        var apiKey = _cfg["Sms:TextLk:ApiKey"] ?? throw new InvalidOperationException("Sms:TextLk:ApiKey missing");
        var senderId = _cfg["Sms:TextLk:SenderId"] ?? throw new InvalidOperationException("Sms:TextLk:SenderId missing");
        var endpoint = _cfg["Sms:TextLk:Endpoint"] ?? "https://app.text.lk/api/v3/sms/send";

        using var client = _httpClientFactory.CreateClient(nameof(TextLkSmsSender));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

        var payload = new
        {
            sender_id = senderId,
            recipient = phoneNumber,
            message
        };

        var json = JsonSerializer.Serialize(payload);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        var resp = await client.PostAsync(endpoint, content, ct);

        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            _log.LogError("Text.lk SMS send failed. Status: {Status} Body: {Body}", resp.StatusCode, body);
            throw new InvalidOperationException("Failed to send SMS.");
        }
    }
}
