using Microsoft.Data.SqlClient;

namespace TalaPress.Infrastructure;

public class ApiEnabledMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;
    private static bool? _cachedEnabled;
    private static DateTime _cacheExpiresUtc = DateTime.MinValue;
    private static readonly object CacheLock = new();

    public ApiEnabledMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        _configuration = configuration;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/api/v1", StringComparison.OrdinalIgnoreCase))
        {
            bool apiEnabled = await IsApiEnabledAsync();
            if (!apiEnabled)
            {
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new { message = "Pearl API is disabled in site settings." });
                return;
            }
        }

        await _next(context);
    }

    private async Task<bool> IsApiEnabledAsync()
    {
        lock (CacheLock)
        {
            if (_cachedEnabled.HasValue && DateTime.UtcNow < _cacheExpiresUtc)
            {
                return _cachedEnabled.Value;
            }
        }

        bool enabled = true;
        string? connectionString = _configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            try
            {
                await using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                const string query = "SELECT ApiEnabled FROM dbo.Settings WHERE Id = 1";
                await using var command = new SqlCommand(query, connection);
                object? result = await command.ExecuteScalarAsync();
                if (result != null && result != DBNull.Value)
                {
                    enabled = Convert.ToBoolean(result);
                }
            }
            catch (SqlException)
            {
                enabled = true;
            }
        }

        lock (CacheLock)
        {
            _cachedEnabled = enabled;
            _cacheExpiresUtc = DateTime.UtcNow.AddMinutes(1);
        }

        return enabled;
    }

    public static void InvalidateCache()
    {
        lock (CacheLock)
        {
            _cachedEnabled = null;
            _cacheExpiresUtc = DateTime.MinValue;
        }
    }
}
