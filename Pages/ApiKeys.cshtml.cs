using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using System.Security.Claims;
using System.Security.Cryptography;
using TalaPress.Api.Security;

namespace TalaPress.Pages;

public class ApiKeysModel : PageModel
{
    private readonly IConfiguration _configuration;

    public ApiKeysModel(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public List<ApiKeyRow> ApiKeys { get; set; } = new();

    [BindProperty]
    public string Name { get; set; } = string.Empty;

    [BindProperty]
    public string? AllowedOrigins { get; set; }

    [BindProperty]
    public DateTime? ExpiresAt { get; set; }

    [TempData]
    public string? SuccessMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    [TempData]
    public string? NewPlainKey { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!CanManageApiKeys())
        {
            return RedirectToPage("/Login");
        }

        string? connectionString = _configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            ErrorMessage = "\u062c\u0645\u0644\u0629 \u0627\u0644\u0627\u062a\u0635\u0627\u0644 \u0628\u0642\u0627\u0639\u062f\u0629 \u0627\u0644\u0628\u064a\u0627\u0646\u0627\u062a \u063a\u064a\u0631 \u0645\u0647\u064a\u0623\u0629.";
            return Page();
        }

        await EnsureApiKeysTableAsync(connectionString);
        await LoadApiKeysAsync(connectionString);
        return Page();
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        if (!CanManageApiKeys())
        {
            return RedirectToPage("/Login");
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "\u0627\u0633\u0645 \u0627\u0644\u0645\u0641\u062a\u0627\u062d \u0645\u0637\u0644\u0648\u0628.";
            return RedirectToPage();
        }

        string? connectionString = _configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            ErrorMessage = "\u062c\u0645\u0644\u0629 \u0627\u0644\u0627\u062a\u0635\u0627\u0644 \u0628\u0642\u0627\u0639\u062f\u0629 \u0627\u0644\u0628\u064a\u0627\u0646\u0627\u062a \u063a\u064a\u0631 \u0645\u0647\u064a\u0623\u0629.";
            return RedirectToPage();
        }

        string plainKey = GeneratePearlKey();
        string keyHash = PearlAuthenticationHandler.ComputeSha256(plainKey);
        string keyPrefix = plainKey[..Math.Min(18, plainKey.Length)];
        long? currentUserId = GetCurrentUserId();

        await EnsureApiKeysTableAsync(connectionString);

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        const string query = @"
            INSERT INTO dbo.ApiKeys (Name, KeyPrefix, KeyHash, AllowedOrigins, IsActive, ExpiresAt, CreatedBy, CreatedAt)
            VALUES (@Name, @KeyPrefix, @KeyHash, @AllowedOrigins, 1, @ExpiresAt, @CreatedBy, GETUTCDATE())";

        await using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@Name", Name.Trim());
        command.Parameters.AddWithValue("@KeyPrefix", keyPrefix);
        command.Parameters.AddWithValue("@KeyHash", keyHash);
        command.Parameters.AddWithValue("@AllowedOrigins", string.IsNullOrWhiteSpace(AllowedOrigins) ? DBNull.Value : AllowedOrigins.Trim());
        command.Parameters.AddWithValue("@ExpiresAt", ExpiresAt.HasValue ? ExpiresAt.Value : DBNull.Value);
        command.Parameters.AddWithValue("@CreatedBy", currentUserId.HasValue ? currentUserId.Value : DBNull.Value);
        await command.ExecuteNonQueryAsync();

        NewPlainKey = plainKey;
        SuccessMessage = "\u062a\u0645 \u0625\u0646\u0634\u0627\u0621 Pearl Key \u0628\u0646\u062c\u0627\u062d. \u0627\u0646\u0633\u062e\u0647 \u0627\u0644\u0622\u0646 \u0644\u0623\u0646\u0647 \u0644\u0646 \u064a\u0638\u0647\u0631 \u0645\u0631\u0629 \u0623\u062e\u0631\u0649.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRevokeAsync(long id)
    {
        if (!CanManageApiKeys())
        {
            return RedirectToPage("/Login");
        }

        string? connectionString = _configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            ErrorMessage = "\u062c\u0645\u0644\u0629 \u0627\u0644\u0627\u062a\u0635\u0627\u0644 \u0628\u0642\u0627\u0639\u062f\u0629 \u0627\u0644\u0628\u064a\u0627\u0646\u0627\u062a \u063a\u064a\u0631 \u0645\u0647\u064a\u0623\u0629.";
            return RedirectToPage();
        }

        long? currentUserId = GetCurrentUserId();
        await EnsureApiKeysTableAsync(connectionString);

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        const string query = @"
            UPDATE dbo.ApiKeys
            SET IsActive = 0, RevokedAt = GETUTCDATE(), RevokedBy = @RevokedBy
            WHERE Id = @Id AND RevokedAt IS NULL";

        await using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@Id", id);
        command.Parameters.AddWithValue("@RevokedBy", currentUserId.HasValue ? currentUserId.Value : DBNull.Value);
        await command.ExecuteNonQueryAsync();

        SuccessMessage = "\u062a\u0645 \u062a\u0639\u0637\u064a\u0644 \u0627\u0644\u0645\u0641\u062a\u0627\u062d \u0628\u0646\u062c\u0627\u062d.";
        return RedirectToPage();
    }

    private bool CanManageApiKeys()
    {
        return User.Identity?.IsAuthenticated == true &&
               (User.HasClaim("Permission", "Settings.Edit") || User.HasClaim("Permission", "Permissions.View"));
    }

    private long? GetCurrentUserId()
    {
        string? idValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(idValue, out long id) ? id : null;
    }

    private static string GeneratePearlKey()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(32);
        return "tp_pearl_" + Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static async Task EnsureApiKeysTableAsync(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        const string query = @"
            IF OBJECT_ID('dbo.ApiKeys', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.ApiKeys(
                    Id BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    Name NVARCHAR(150) NOT NULL,
                    KeyPrefix NVARCHAR(50) NOT NULL,
                    KeyHash NVARCHAR(128) NOT NULL,
                    AllowedOrigins NVARCHAR(1000) NULL,
                    IsActive BIT NOT NULL CONSTRAINT DF_ApiKeys_IsActive DEFAULT 1,
                    ExpiresAt DATETIME2 NULL,
                    LastUsedAt DATETIME2 NULL,
                    CreatedBy BIGINT NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_ApiKeys_CreatedAt DEFAULT GETUTCDATE(),
                    RevokedAt DATETIME2 NULL,
                    RevokedBy BIGINT NULL
                );

                CREATE UNIQUE INDEX UX_ApiKeys_KeyHash ON dbo.ApiKeys(KeyHash);
                CREATE INDEX IX_ApiKeys_IsActive ON dbo.ApiKeys(IsActive, ExpiresAt);
            END";

        await using var command = new SqlCommand(query, connection);
        await command.ExecuteNonQueryAsync();
    }

    private async Task LoadApiKeysAsync(string connectionString)
    {
        ApiKeys.Clear();

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        const string query = @"
            SELECT k.Id, k.Name, k.KeyPrefix, k.AllowedOrigins, k.IsActive, k.ExpiresAt, k.LastUsedAt,
                   k.CreatedAt, k.RevokedAt, u.FullName
            FROM dbo.ApiKeys k
            LEFT JOIN dbo.Users u ON k.CreatedBy = u.Id
            ORDER BY k.CreatedAt DESC";

        await using var command = new SqlCommand(query, connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            ApiKeys.Add(new ApiKeyRow
            {
                Id = reader.GetInt64(0),
                Name = reader.GetString(1),
                KeyPrefix = reader.GetString(2),
                AllowedOrigins = reader.IsDBNull(3) ? null : reader.GetString(3),
                IsActive = reader.GetBoolean(4),
                ExpiresAt = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                LastUsedAt = reader.IsDBNull(6) ? null : reader.GetDateTime(6),
                CreatedAt = reader.GetDateTime(7),
                RevokedAt = reader.IsDBNull(8) ? null : reader.GetDateTime(8),
                CreatedByName = reader.IsDBNull(9) ? "System" : reader.GetString(9)
            });
        }
    }

    public class ApiKeyRow
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string KeyPrefix { get; set; } = string.Empty;
        public string? AllowedOrigins { get; set; }
        public bool IsActive { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public DateTime? LastUsedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? RevokedAt { get; set; }
        public string CreatedByName { get; set; } = string.Empty;
    }
}
