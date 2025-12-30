using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;

namespace TripShare.Api.Filters;

[AttributeUsage(AttributeTargets.Method)]
public sealed class IdempotencyKeyAttribute : Attribute, IAsyncActionFilter
{
    private const string HeaderName = "Idempotency-Key";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly TimeSpan _ttl;

    public IdempotencyKeyAttribute(int secondsToLive = 300)
    {
        _ttl = TimeSpan.FromSeconds(secondsToLive);
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var cache = context.HttpContext.RequestServices.GetRequiredService<IMemoryCache>();
        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<IdempotencyKeyAttribute>>();

        if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var keyValues))
        {
            await next();
            return;
        }

        var key = keyValues.FirstOrDefault()?.Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            context.Result = new BadRequestObjectResult(new
            {
                error = "idempotency_key_required",
                message = "Idempotency-Key header cannot be empty."
            });
            return;
        }

        if (cache.TryGetValue<IdempotencyCacheEntry>(BuildCacheKey(context, key), out var cached) && cached is not null)
        {
            logger.LogInformation("Returning cached response for idempotency key {Key}", key);
            context.Result = ToActionResult(cached);
            return;
        }

        var executed = await next();
        if (executed.Result is null)
        {
            return;
        }

        var entry = FromActionResult(executed.Result);
        cache.Set(BuildCacheKey(context, key), entry, _ttl);
    }

    private static string BuildCacheKey(ActionContext ctx, string key)
        => $"idem:{ctx.HttpContext.Request.Path}:{key}";

    private static IdempotencyCacheEntry FromActionResult(IActionResult result)
    {
        return result switch
        {
            ObjectResult o => new IdempotencyCacheEntry(o.StatusCode ?? StatusCodes.Status200OK, o.Value),
            StatusCodeResult s => new IdempotencyCacheEntry(s.StatusCode, null),
            _ => new IdempotencyCacheEntry(StatusCodes.Status200OK, result)
        };
    }

    private static IActionResult ToActionResult(IdempotencyCacheEntry entry)
        => entry.Body is null
            ? new StatusCodeResult(entry.StatusCode)
            : new ContentResult
            {
                StatusCode = entry.StatusCode,
                Content = JsonSerializer.Serialize(entry.Body, JsonOptions),
                ContentType = "application/json"
            };

    private sealed record IdempotencyCacheEntry(int StatusCode, object? Body);
}
