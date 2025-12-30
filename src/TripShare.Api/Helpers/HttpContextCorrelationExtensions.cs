using Microsoft.AspNetCore.Http;

namespace TripShare.Api.Helpers;

public static class HttpContextCorrelationExtensions
{
    private const string CorrelationHeader = "X-Correlation-Id";

    public static string GetCorrelationId(this HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(CorrelationHeader, out var cid) &&
            !string.IsNullOrWhiteSpace(cid))
        {
            return cid!;
        }

        return context.TraceIdentifier;
    }
}
