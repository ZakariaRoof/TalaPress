using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TalaPress.Api.Security;

namespace TalaPress.Api.Controllers;

[ApiController]
[Route("api/v1/cart")]
[Authorize(AuthenticationSchemes = PearlAuthenticationDefaults.AuthenticationScheme)]
public sealed class CartController : ControllerBase
{
    private const string DefaultVisitorPrefix = "pearl";
    private const string PendingStatus = "pending";
    private const string SyncedStatus = "synced";
    private const string RemovedStatus = "removed";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<CartController> _logger;

    public CartController(IConfiguration configuration, IHttpClientFactory httpClientFactory, ILogger<CartController> logger)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetCart(CancellationToken cancellationToken = default)
    {
        var visitorId = ResolveVisitorId();
        var items = await LoadCartItemsAsync(visitorId, cancellationToken);
        return Ok(BuildCartResponse(items));
    }

    [HttpGet("checkout-url")]
    public async Task<IActionResult> GetCheckoutUrl(CancellationToken cancellationToken = default)
    {
        var visitorId = ResolveVisitorId();
        var items = await LoadCartItemsAsync(visitorId, cancellationToken);
        var response = BuildCartResponse(items);
        return Ok(new { response.CheckoutUrl, response.CanCheckout, response.DonationIds });
    }

    [HttpPost("items")]
    public async Task<IActionResult> CreateCartItem([FromBody] CreateCartItemRequest request, CancellationToken cancellationToken = default)
    {
        var validationMessage = ValidateCreateRequest(request);
        if (!string.IsNullOrWhiteSpace(validationMessage))
        {
            return BadRequest(new CartMutationResponse
            {
                Success = false,
                Code = "VALIDATION_FAILED",
                Message = validationMessage
            });
        }

        var visitorId = ResolveVisitorId();
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var existing = await FindByIdempotencyKeyAsync(visitorId, request.IdempotencyKey.Trim(), cancellationToken);
            if (existing is not null)
            {
                var existingCart = await LoadCartItemsAsync(visitorId, cancellationToken);
                return Ok(BuildMutationResponse(true, existing, existingCart, "تمت إضافة التبرع إلى السلة."));
            }
        }

        var cartItem = await InsertPendingCartItemAsync(visitorId, request, cancellationToken);
        var items = await LoadCartItemsAsync(visitorId, cancellationToken);
        return Ok(BuildMutationResponse(true, cartItem, items, "تمت إضافة التبرع إلى السلة."));
    }

    [HttpDelete("items/{id}")]
    public async Task<IActionResult> DeleteCartItem(string id, CancellationToken cancellationToken = default)
    {
        var visitorId = ResolveVisitorId();
        if (!Guid.TryParse(id, out var cartItemId))
        {
            return NotFound(new DeleteCartItemResponse
            {
                Success = false,
                Code = "CART_ITEM_NOT_FOUND",
                Message = "تعذر العثور على عنصر السلة."
            });
        }

        var item = await FindCartItemAsync(visitorId, cartItemId, cancellationToken);
        if (item is null)
        {
            return NotFound(new DeleteCartItemResponse
            {
                Success = false,
                Code = "CART_ITEM_NOT_FOUND",
                Message = "تعذر العثور على عنصر السلة."
            });
        }

        if (!string.IsNullOrWhiteSpace(item.QcDonationIds))
        {
            await RemoveFromQatarCharityAsync(item.QcDonationIds, cancellationToken);
        }

        await MarkRemovedAsync(visitorId, cartItemId, cancellationToken);

        var remainingItems = await LoadCartItemsAsync(visitorId, cancellationToken);
        var remainingCart = BuildCartResponse(remainingItems);
        return Ok(new DeleteCartItemResponse
        {
            Success = true,
            Message = "تم حذف عنصر السلة.",
            RemovedLocalItemId = id,
            RemovedQcDonationIds = item.QcDonationIds,
            RemainingDonationIds = remainingCart.DonationIds,
            CheckoutUrl = remainingCart.CheckoutUrl
        });
    }

    [HttpDelete]
    public async Task<IActionResult> ClearCart(CancellationToken cancellationToken = default)
    {
        var visitorId = ResolveVisitorId();
        var items = await LoadCartItemsAsync(visitorId, cancellationToken);
        var donationIds = JoinDonationIds(items);
        if (!string.IsNullOrWhiteSpace(donationIds))
        {
            await RemoveFromQatarCharityAsync(donationIds, cancellationToken);
        }

        await MarkAllRemovedAsync(visitorId, cancellationToken);
        return Ok(new DeleteCartItemResponse
        {
            Success = true,
            Message = "تم تفريغ السلة.",
            RemovedQcDonationIds = donationIds,
            RemainingDonationIds = string.Empty,
            CheckoutUrl = null
        });
    }

    private async Task<CartItemRecord> InsertPendingCartItemAsync(string visitorId, CreateCartItemRequest request, CancellationToken cancellationToken)
    {
        var item = new CartItemRecord
        {
            Id = Guid.NewGuid(),
            QcDonationId = null,
            QcDonationIds = null,
            Label = NormalizeLabel(request.Label),
            DonationType = NormalizeDonationType(request.DonationType),
            AccountTypeId = request.AccountTypeId,
            CountryId = request.CountryId <= 0 ? 542 : request.CountryId,
            Amount = request.Amount,
            CurrencyId = request.CurrencyId is > 0 ? request.CurrencyId.Value : 1,
            PeriodTypeId = request.PeriodTypeId,
            Source = string.IsNullOrWhiteSpace(request.Source) ? "quick-donation" : request.Source.Trim(),
            SyncStatus = PendingStatus,
            PaidForId = request.PaidForId,
            SponsorshipCategoryId = request.SponsorshipCategoryId,
            IsYearly = request.IsYearly
        };

        var connectionString = GetConnectionString();
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        const string query = @"
            INSERT INTO dbo.AlakraboonCartItems
                (Id, VisitorId, UserId, QcDonationId, QcDonationIds, Label, DonationType, AccountTypeId, CountryId, Amount, CurrencyId, PeriodTypeId, Source, IdempotencyKey, QcPayloadJson, QcResponseJson, SyncStatus, ErrorMessage, CreatedAt, UpdatedAt, PaidForId, SponsorshipCategoryId, IsYearly)
            VALUES
                (@Id, @VisitorId, NULL, NULL, NULL, @Label, @DonationType, @AccountTypeId, @CountryId, @Amount, @CurrencyId, @PeriodTypeId, @Source, @IdempotencyKey, @QcPayloadJson, NULL, @SyncStatus, NULL, SYSUTCDATETIME(), SYSUTCDATETIME(), @PaidForId, @SponsorshipCategoryId, @IsYearly);";

        await using var command = new SqlCommand(query, connection);
        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = item.Id;
        command.Parameters.Add("@VisitorId", SqlDbType.NVarChar, 100).Value = visitorId;
        command.Parameters.Add("@Label", SqlDbType.NVarChar, 300).Value = (object?)item.Label ?? DBNull.Value;
        command.Parameters.Add("@DonationType", SqlDbType.NVarChar, 20).Value = item.DonationType;
        command.Parameters.Add("@AccountTypeId", SqlDbType.Int).Value = item.AccountTypeId;
        command.Parameters.Add("@CountryId", SqlDbType.Int).Value = item.CountryId;
        command.Parameters.Add("@Amount", SqlDbType.Decimal).Value = item.Amount;
        command.Parameters.Add("@CurrencyId", SqlDbType.Int).Value = item.CurrencyId;
        command.Parameters.Add("@PeriodTypeId", SqlDbType.Int).Value = (object?)item.PeriodTypeId ?? DBNull.Value;
        command.Parameters.Add("@Source", SqlDbType.NVarChar, 100).Value = (object?)item.Source ?? DBNull.Value;
        command.Parameters.Add("@IdempotencyKey", SqlDbType.NVarChar, 100).Value = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? DBNull.Value : request.IdempotencyKey.Trim();
        command.Parameters.Add("@QcPayloadJson", SqlDbType.NVarChar).Value = JsonSerializer.Serialize(request, JsonOptions);
        command.Parameters.Add("@SyncStatus", SqlDbType.NVarChar, 30).Value = item.SyncStatus;
        command.Parameters.Add("@PaidForId", SqlDbType.Int).Value = (object?)item.PaidForId ?? DBNull.Value;
        command.Parameters.Add("@SponsorshipCategoryId", SqlDbType.Int).Value = (object?)item.SponsorshipCategoryId ?? DBNull.Value;
        command.Parameters.Add("@IsYearly", SqlDbType.Bit).Value = item.IsYearly;
        await command.ExecuteNonQueryAsync(cancellationToken);

        return item;
    }

    private async Task<IReadOnlyList<CartItemRecord>> LoadCartItemsAsync(string visitorId, CancellationToken cancellationToken)
    {
        var connectionString = GetConnectionString();
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        const string query = @"
            SELECT Id, QcDonationId, QcDonationIds, Label, DonationType, AccountTypeId, CountryId, Amount, CurrencyId, PeriodTypeId, Source, SyncStatus, PaidForId, SponsorshipCategoryId, IsYearly
            FROM dbo.AlakraboonCartItems
            WHERE VisitorId = @VisitorId
              AND SyncStatus <> @RemovedStatus
            ORDER BY CreatedAt ASC;";

        await using var command = new SqlCommand(query, connection);
        command.Parameters.Add("@VisitorId", SqlDbType.NVarChar, 100).Value = visitorId;
        command.Parameters.Add("@RemovedStatus", SqlDbType.NVarChar, 30).Value = RemovedStatus;

        var items = new List<CartItemRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(ReadCartItem(reader));
        }

        return items;
    }

    private async Task<CartItemRecord?> FindCartItemAsync(string visitorId, Guid id, CancellationToken cancellationToken)
    {
        var connectionString = GetConnectionString();
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        const string query = @"
            SELECT TOP (1) Id, QcDonationId, QcDonationIds, Label, DonationType, AccountTypeId, CountryId, Amount, CurrencyId, PeriodTypeId, Source, SyncStatus, PaidForId, SponsorshipCategoryId, IsYearly
            FROM dbo.AlakraboonCartItems
            WHERE VisitorId = @VisitorId
              AND Id = @Id
              AND SyncStatus <> @RemovedStatus;";

        await using var command = new SqlCommand(query, connection);
        command.Parameters.Add("@VisitorId", SqlDbType.NVarChar, 100).Value = visitorId;
        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = id;
        command.Parameters.Add("@RemovedStatus", SqlDbType.NVarChar, 30).Value = RemovedStatus;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadCartItem(reader) : null;
    }

    private async Task<CartItemRecord?> FindByIdempotencyKeyAsync(string visitorId, string idempotencyKey, CancellationToken cancellationToken)
    {
        var connectionString = GetConnectionString();
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        const string query = @"
            SELECT TOP (1) Id, QcDonationId, QcDonationIds, Label, DonationType, AccountTypeId, CountryId, Amount, CurrencyId, PeriodTypeId, Source, SyncStatus, PaidForId, SponsorshipCategoryId, IsYearly
            FROM dbo.AlakraboonCartItems
            WHERE VisitorId = @VisitorId
              AND IdempotencyKey = @IdempotencyKey
              AND SyncStatus <> @RemovedStatus
            ORDER BY CreatedAt DESC;";

        await using var command = new SqlCommand(query, connection);
        command.Parameters.Add("@VisitorId", SqlDbType.NVarChar, 100).Value = visitorId;
        command.Parameters.Add("@IdempotencyKey", SqlDbType.NVarChar, 100).Value = idempotencyKey;
        command.Parameters.Add("@RemovedStatus", SqlDbType.NVarChar, 30).Value = RemovedStatus;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadCartItem(reader) : null;
    }

    private async Task MarkRemovedAsync(string visitorId, Guid id, CancellationToken cancellationToken)
    {
        var connectionString = GetConnectionString();
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        const string query = @"
            UPDATE dbo.AlakraboonCartItems
            SET SyncStatus = @RemovedStatus,
                UpdatedAt = SYSUTCDATETIME()
            WHERE VisitorId = @VisitorId
              AND Id = @Id;";

        await using var command = new SqlCommand(query, connection);
        command.Parameters.Add("@RemovedStatus", SqlDbType.NVarChar, 30).Value = RemovedStatus;
        command.Parameters.Add("@VisitorId", SqlDbType.NVarChar, 100).Value = visitorId;
        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = id;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task MarkAllRemovedAsync(string visitorId, CancellationToken cancellationToken)
    {
        var connectionString = GetConnectionString();
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        const string query = @"
            UPDATE dbo.AlakraboonCartItems
            SET SyncStatus = @RemovedStatus,
                UpdatedAt = SYSUTCDATETIME()
            WHERE VisitorId = @VisitorId
              AND SyncStatus <> @RemovedStatus;";

        await using var command = new SqlCommand(query, connection);
        command.Parameters.Add("@RemovedStatus", SqlDbType.NVarChar, 30).Value = RemovedStatus;
        command.Parameters.Add("@VisitorId", SqlDbType.NVarChar, 100).Value = visitorId;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task RemoveFromQatarCharityAsync(string donationIds, CancellationToken cancellationToken)
    {
        try
        {
            var baseUrl = GetCheckoutBaseUrl();
            var removePath = _configuration["QatarCharityCart:RemoveBasketCheckoutPath"];
            if (string.IsNullOrWhiteSpace(removePath))
            {
                removePath = "/ar/qa/Donation/RemoveCheckout";
            }

            var endpoint = new Uri(new Uri(baseUrl.TrimEnd('/') + "/"), removePath.TrimStart('/'));
            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["donationIds"] = donationIds
            });

            using var client = _httpClientFactory.CreateClient();
            using var response = await client.PostAsync(endpoint, content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("QC RemoveCheckout returned status {StatusCode} for donationIds {DonationIds}.", (int)response.StatusCode, donationIds);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to call QC RemoveCheckout for donationIds {DonationIds}.", donationIds);
        }
    }

    private CartResponse BuildCartResponse(IReadOnlyList<CartItemRecord> items)
    {
        var donationIds = JoinDonationIds(items);
        var canCheckout = UseBrowserSessionCheckoutSync()
            ? items.Count > 0
            : !string.IsNullOrWhiteSpace(donationIds);

        return new CartResponse
        {
            Items = items.Select(ToDto).ToArray(),
            DonationIds = donationIds,
            TotalAmount = items.Sum(item => item.Amount),
            CanCheckout = canCheckout,
            CheckoutUrl = canCheckout ? BuildCheckoutUrl(items, donationIds) : null
        };
    }

    private CartMutationResponse BuildMutationResponse(bool success, CartItemRecord cartItem, IReadOnlyList<CartItemRecord> items, string message)
    {
        var cart = BuildCartResponse(items);
        return new CartMutationResponse
        {
            Success = success,
            Message = message,
            CartItem = ToDto(cartItem),
            DonationIds = cart.DonationIds,
            TotalAmount = cart.TotalAmount,
            CanCheckout = cart.CanCheckout,
            CheckoutUrl = cart.CheckoutUrl
        };
    }

    private string? BuildCheckoutUrl(IReadOnlyList<CartItemRecord> items, string donationIds)
    {
        if (UseBrowserSessionCheckoutSync())
        {
            var payload = new CheckoutSyncPayload
            {
                Version = 1,
                IssuedAtUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Items = items.Select(item => new CheckoutSyncItem
                {
                    DonationType = item.DonationType,
                    AccountTypeId = item.AccountTypeId,
                    CountryId = item.CountryId,
                    Amount = item.Amount,
                    CurrencyId = item.CurrencyId,
                    PeriodTypeId = item.PeriodTypeId ?? 5,
                    PaidForId = item.PaidForId,
                    SponsorshipCategoryId = item.SponsorshipCategoryId,
                    IsYearly = item.IsYearly,
                    Label = item.Label
                }).ToArray()
            };

            var payloadJson = JsonSerializer.Serialize(payload, JsonOptions);
            var encodedPayload = Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson));
            var signature = SignPayload(encodedPayload);
            var path = _configuration["QatarCharityCart:AlakraboonSyncPath"];
            if (string.IsNullOrWhiteSpace(path))
            {
                path = "/ar/qa/checkout/alakraboonsync";
            }

            return GetCheckoutBaseUrl().TrimEnd('/') + path + "?payload=" + Uri.EscapeDataString(encodedPayload) + "&signature=" + Uri.EscapeDataString(signature) + "&source=alakraboon";
        }

        if (string.IsNullOrWhiteSpace(donationIds))
        {
            return null;
        }

        var checkoutPath = _configuration["QatarCharityCart:CheckoutPath"];
        if (string.IsNullOrWhiteSpace(checkoutPath))
        {
            checkoutPath = "/ar/qa/checkout/index";
        }

        return GetCheckoutBaseUrl().TrimEnd('/') + checkoutPath + "?donationId=" + Uri.EscapeDataString(donationIds) + "&source=alakraboon";
    }

    private string SignPayload(string encodedPayload)
    {
        var signingKey = _configuration["QatarCharityCart:AlakraboonSyncSigningKey"];
        if (string.IsNullOrWhiteSpace(signingKey))
        {
            signingKey = "alakraboon-dev-sync-key-change-me";
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(signingKey));
        return Base64UrlEncode(hmac.ComputeHash(Encoding.UTF8.GetBytes(encodedPayload)));
    }

    private bool UseBrowserSessionCheckoutSync()
    {
        return _configuration.GetValue("QatarCharityCart:UseBrowserSessionSync", true);
    }

    private string GetCheckoutBaseUrl()
    {
        var baseUrl = _configuration["QatarCharityCart:CheckoutBaseUrl"];
        return string.IsNullOrWhiteSpace(baseUrl) ? "https://www.qcharity.org" : baseUrl;
    }

    private string GetConnectionString()
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Database connection is not configured.");
        }

        return connectionString;
    }

    private string ResolveVisitorId()
    {
        if (Request.Headers.TryGetValue("X-Visitor-Id", out var visitorHeader) && !string.IsNullOrWhiteSpace(visitorHeader.ToString()))
        {
            return TrimVisitorId(visitorHeader.ToString());
        }

        var apiKeyId = User.FindFirstValue("ApiKeyId");
        return TrimVisitorId(DefaultVisitorPrefix + "-" + (string.IsNullOrWhiteSpace(apiKeyId) ? "anonymous" : apiKeyId));
    }

    private static string TrimVisitorId(string value)
    {
        var normalized = value.Trim();
        return normalized.Length <= 100 ? normalized : normalized[..100];
    }

    private static string ValidateCreateRequest(CreateCartItemRequest request)
    {
        if (request is null)
        {
            return "بيانات السلة غير صحيحة.";
        }

        var donationType = NormalizeDonationType(request.DonationType);
        if (donationType is not ("single" or "periodic" or "sponsorship"))
        {
            return "نوع التبرع غير صحيح.";
        }

        if (request.AccountTypeId <= 0)
        {
            return "يرجى اختيار نوع التبرع.";
        }

        if (request.Amount <= 0)
        {
            return "يرجى إدخال مبلغ صحيح.";
        }

        if (donationType == "sponsorship" && !(request.PaidForId > 0))
        {
            return "تعذر تحديد الحالة المطلوب كفالتها.";
        }

        if (donationType == "periodic" && !request.PeriodTypeId.HasValue)
        {
            return "يرجى اختيار فترة التبرع الدوري.";
        }

        if (donationType == "periodic" && request.Amount <= 9)
        {
            return "الحد الأدنى للتبرع الدوري أكبر من 9 ر.ق.";
        }

        return string.Empty;
    }

    private static string NormalizeDonationType(string? donationType)
    {
        return string.IsNullOrWhiteSpace(donationType) ? "single" : donationType.Trim().ToLowerInvariant();
    }

    private static string NormalizeLabel(string? label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return "تبرع";
        }

        var normalized = label.Trim();
        return normalized.Length <= 300 ? normalized : normalized[..300];
    }

    private static string JoinDonationIds(IReadOnlyList<CartItemRecord> items)
    {
        return string.Join(",", items
            .Where(item => string.Equals(item.SyncStatus, SyncedStatus, StringComparison.OrdinalIgnoreCase))
            .Select(item => item.QcDonationIds)
            .Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static CartItemDto ToDto(CartItemRecord item)
    {
        return new CartItemDto
        {
            Id = item.Id.ToString("D"),
            QcDonationId = item.QcDonationId,
            QcDonationIds = item.QcDonationIds,
            Label = item.Label,
            DonationType = item.DonationType,
            AccountTypeId = item.AccountTypeId,
            CountryId = item.CountryId,
            Amount = item.Amount,
            CurrencyId = item.CurrencyId,
            CurrencyLabel = "ر.ق",
            PeriodTypeId = item.PeriodTypeId,
            PaidForId = item.PaidForId,
            SponsorshipCategoryId = item.SponsorshipCategoryId,
            IsYearly = item.IsYearly,
            Source = item.Source,
            SyncStatus = item.SyncStatus
        };
    }

    private static CartItemRecord ReadCartItem(SqlDataReader reader)
    {
        return new CartItemRecord
        {
            Id = reader.GetGuid(reader.GetOrdinal("Id")),
            QcDonationId = ReadNullableInt64(reader, "QcDonationId"),
            QcDonationIds = ReadNullableString(reader, "QcDonationIds"),
            Label = ReadNullableString(reader, "Label") ?? "تبرع",
            DonationType = ReadNullableString(reader, "DonationType") ?? "single",
            AccountTypeId = reader.GetInt32(reader.GetOrdinal("AccountTypeId")),
            CountryId = reader.GetInt32(reader.GetOrdinal("CountryId")),
            Amount = reader.GetDecimal(reader.GetOrdinal("Amount")),
            CurrencyId = reader.GetInt32(reader.GetOrdinal("CurrencyId")),
            PeriodTypeId = ReadNullableInt32(reader, "PeriodTypeId"),
            Source = ReadNullableString(reader, "Source") ?? string.Empty,
            SyncStatus = ReadNullableString(reader, "SyncStatus") ?? PendingStatus,
            PaidForId = ReadNullableInt32(reader, "PaidForId"),
            SponsorshipCategoryId = ReadNullableInt32(reader, "SponsorshipCategoryId"),
            IsYearly = reader.GetBoolean(reader.GetOrdinal("IsYearly"))
        };
    }

    private static string? ReadNullableString(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static int? ReadNullableInt32(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }

    private static long? ReadNullableInt64(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt64(ordinal);
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public sealed class CreateCartItemRequest
    {
        public string DonationType { get; set; } = "single";
        public int AccountTypeId { get; set; }
        public int CountryId { get; set; } = 542;
        public decimal Amount { get; set; }
        public int? PeriodTypeId { get; set; }
        public int? CurrencyId { get; set; }
        public int? PaidForId { get; set; }
        public int? SponsorshipCategoryId { get; set; }
        public bool IsYearly { get; set; }
        public string? Label { get; set; }
        public string Source { get; set; } = "quick-donation";
        public string? IdempotencyKey { get; set; }
    }

    public sealed class CartItemDto
    {
        public string Id { get; set; } = string.Empty;
        public string LocalCartId => Id;
        public long? QcDonationId { get; set; }
        public string? QcDonationIds { get; set; }
        public string Label { get; set; } = "تبرع";
        public string DonationType { get; set; } = "single";
        public int AccountTypeId { get; set; }
        public int CountryId { get; set; }
        public decimal Amount { get; set; }
        public int? CurrencyId { get; set; }
        public string CurrencyLabel { get; set; } = "ر.ق";
        public int? PeriodTypeId { get; set; }
        public int? PaidForId { get; set; }
        public int? SponsorshipCategoryId { get; set; }
        public bool IsYearly { get; set; }
        public string Source { get; set; } = string.Empty;
        public string SyncStatus { get; set; } = string.Empty;
    }

    public sealed class CartMutationResponse
    {
        public bool Success { get; set; }
        public string? Code { get; set; }
        public string? Message { get; set; }
        public CartItemDto? CartItem { get; set; }
        public string? DonationIds { get; set; }
        public decimal? TotalAmount { get; set; }
        public bool? CanCheckout { get; set; }
        public string? CheckoutUrl { get; set; }
    }

    public sealed class CartResponse
    {
        public IReadOnlyList<CartItemDto> Items { get; set; } = Array.Empty<CartItemDto>();
        public string? DonationIds { get; set; }
        public decimal TotalAmount { get; set; }
        public bool CanCheckout { get; set; }
        public string? CheckoutUrl { get; set; }
    }

    public sealed class DeleteCartItemResponse
    {
        public bool Success { get; set; }
        public string? Code { get; set; }
        public string? Message { get; set; }
        public string? RemovedLocalItemId { get; set; }
        public string? RemovedQcDonationIds { get; set; }
        public string? RemainingDonationIds { get; set; }
        public string? CheckoutUrl { get; set; }
    }

    private sealed class CartItemRecord
    {
        public Guid Id { get; set; }
        public long? QcDonationId { get; set; }
        public string? QcDonationIds { get; set; }
        public string Label { get; set; } = "تبرع";
        public string DonationType { get; set; } = "single";
        public int AccountTypeId { get; set; }
        public int CountryId { get; set; }
        public decimal Amount { get; set; }
        public int CurrencyId { get; set; } = 1;
        public int? PeriodTypeId { get; set; }
        public string Source { get; set; } = string.Empty;
        public string SyncStatus { get; set; } = PendingStatus;
        public int? PaidForId { get; set; }
        public int? SponsorshipCategoryId { get; set; }
        public bool IsYearly { get; set; }
    }

    private sealed class CheckoutSyncPayload
    {
        public int Version { get; set; }
        public long IssuedAtUtc { get; set; }
        public IReadOnlyList<CheckoutSyncItem> Items { get; set; } = Array.Empty<CheckoutSyncItem>();
    }

    private sealed class CheckoutSyncItem
    {
        public string DonationType { get; set; } = "single";
        public int AccountTypeId { get; set; }
        public int CountryId { get; set; }
        public decimal Amount { get; set; }
        public int CurrencyId { get; set; }
        public int PeriodTypeId { get; set; }
        public int? PaidForId { get; set; }
        public int? SponsorshipCategoryId { get; set; }
        public bool IsYearly { get; set; }
        public string Label { get; set; } = "تبرع";
    }
}