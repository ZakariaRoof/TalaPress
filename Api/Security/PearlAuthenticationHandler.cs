using Microsoft.AspNetCore.Authentication;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;

namespace TalaPress.Api.Security;

public static class PearlAuthenticationDefaults
{
    public const string AuthenticationScheme = "Pearl";
}

public class PearlAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IConfiguration _configuration;

    public PearlAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IConfiguration configuration) : base(options, logger, encoder)
    {
        _configuration = configuration;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? rawKey = null;

        string authorization = Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(authorization) && authorization.StartsWith("Pearl ", StringComparison.OrdinalIgnoreCase))
        {
            rawKey = authorization[6..].Trim();
        }

        if (string.IsNullOrWhiteSpace(rawKey) && Request.Headers.TryGetValue("X-Pearl-Key", out var headerValues))
        {
            rawKey = headerValues.FirstOrDefault();
        }

        if (string.IsNullOrWhiteSpace(rawKey))
        {
            return AuthenticateResult.NoResult();
        }

        string? connectionString = _configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return AuthenticateResult.Fail("Database connection is not configured.");
        }

        try
        {
            string keyHash = ComputeSha256(rawKey);

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            const string query = @"
                SELECT Id, Name
                FROM dbo.ApiKeys
                WHERE KeyHash = @KeyHash
                  AND IsActive = 1
                  AND RevokedAt IS NULL
                  AND (ExpiresAt IS NULL OR ExpiresAt > GETUTCDATE())";

            long keyId;
            string keyName;

            await using (var command = new SqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@KeyHash", keyHash);
                await using var reader = await command.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                {
                    return AuthenticateResult.Fail("Invalid Pearl key.");
                }

                keyId = reader.GetInt64(0);
                keyName = reader.GetString(1);
            }

            const string updateQuery = "UPDATE dbo.ApiKeys SET LastUsedAt = GETUTCDATE() WHERE Id = @Id";
            await using (var updateCommand = new SqlCommand(updateQuery, connection))
            {
                updateCommand.Parameters.AddWithValue("@Id", keyId);
                await updateCommand.ExecuteNonQueryAsync();
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, keyId.ToString()),
                new(ClaimTypes.Name, keyName),
                new("ApiKeyId", keyId.ToString()),
                new("ApiKeyName", keyName)
            };

            var identity = new ClaimsIdentity(claims, PearlAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, PearlAuthenticationDefaults.AuthenticationScheme);

            return AuthenticateResult.Success(ticket);
        }
        catch (SqlException)
        {
            return AuthenticateResult.Fail("Pearl key store is not ready.");
        }
    }

    public static string ComputeSha256(string value)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
