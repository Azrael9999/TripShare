namespace TripShare.Api.Middleware;

public sealed class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task Invoke(HttpContext context)
    {
        const string header = "X-Correlation-Id";
        if (!context.Request.Headers.TryGetValue(header, out var cid) || string.IsNullOrWhiteSpace(cid))
        {
            cid = Guid.NewGuid().ToString("N");
            context.Request.Headers[header] = cid!;
        }
        context.Response.Headers[header] = cid!;
        await _next(context);
    }
}
