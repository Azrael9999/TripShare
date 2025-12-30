using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace TripShare.Api.HealthChecks;

public sealed class SqlConnectionHealthCheck : IHealthCheck
{
    private readonly string _connectionString;
    private readonly ILogger<SqlConnectionHealthCheck> _logger;

    public SqlConnectionHealthCheck(string connectionString, ILogger<SqlConnectionHealthCheck> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);

            await using var cmd = new SqlCommand("SELECT 1", conn);
            await cmd.ExecuteScalarAsync(cancellationToken);

            return HealthCheckResult.Healthy("SQL connectivity OK");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SQL health check failed");
            return HealthCheckResult.Unhealthy("SQL connectivity failed", ex);
        }
    }
}
