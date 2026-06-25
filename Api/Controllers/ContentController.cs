using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Text.Json;
using System.Text.RegularExpressions;
using TalaPress.Api.Security;

namespace TalaPress.Api.Controllers;

[ApiController]
[Route("api/v1/content")]
[Authorize(AuthenticationSchemes = PearlAuthenticationDefaults.AuthenticationScheme)]
public class ContentController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public ContentController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? q = null,
        [FromQuery] string? search = null,
        [FromQuery] long? contentTypeId = null,
        [FromQuery] string? contentType = null,
        [FromQuery] long? categoryId = null,
        [FromQuery] long? subCategoryId = null,
        [FromQuery] string? status = "Published",
        [FromQuery] string? slug = null,
        [FromQuery] long? authorId = null,
        [FromQuery] DateTime? publishDateFrom = null,
        [FromQuery] DateTime? publishDateTo = null,
        [FromQuery] DateTime? createdFrom = null,
        [FromQuery] DateTime? createdTo = null,
        [FromQuery] bool? hasFeaturedImage = null,
        [FromQuery] string? sortBy = "publishDate",
        [FromQuery] string? sortDir = "desc")
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        string? freeText = string.IsNullOrWhiteSpace(q) ? search : q;

        string? connectionString = _configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return Problem("Database connection is not configured.");
        }

        var where = new List<string> { "c.IsDeleted = 0" };
        var parameters = new List<SqlParameter>();

        if (contentTypeId.HasValue && contentTypeId.Value > 0)
        {
            where.Add("c.ContentTypeId = @ContentTypeId");
            parameters.Add(new SqlParameter("@ContentTypeId", contentTypeId.Value));
        }

        if (!string.IsNullOrWhiteSpace(contentType))
        {
            where.Add("(ct.Name = @ContentType OR ct.Name_En = @ContentType)");
            parameters.Add(new SqlParameter("@ContentType", contentType.Trim()));
        }

        if (categoryId.HasValue && categoryId.Value > 0)
        {
            where.Add("c.CategoryId = @CategoryId");
            parameters.Add(new SqlParameter("@CategoryId", categoryId.Value));
        }

        if (subCategoryId.HasValue && subCategoryId.Value > 0)
        {
            where.Add("c.SubCategoryId = @SubCategoryId");
            parameters.Add(new SqlParameter("@SubCategoryId", subCategoryId.Value));
        }

        if (!string.IsNullOrWhiteSpace(status) && !status.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            where.Add("c.Status = @Status");
            parameters.Add(new SqlParameter("@Status", status));
        }

        if (!string.IsNullOrWhiteSpace(slug))
        {
            where.Add("c.Slug = @Slug");
            parameters.Add(new SqlParameter("@Slug", slug.Trim()));
        }

        if (authorId.HasValue && authorId.Value > 0)
        {
            where.Add("c.CreatedBy = @AuthorId");
            parameters.Add(new SqlParameter("@AuthorId", authorId.Value));
        }

        if (publishDateFrom.HasValue)
        {
            where.Add("c.PublishDate >= @PublishDateFrom");
            parameters.Add(new SqlParameter("@PublishDateFrom", publishDateFrom.Value));
        }

        if (publishDateTo.HasValue)
        {
            where.Add("c.PublishDate <= @PublishDateTo");
            parameters.Add(new SqlParameter("@PublishDateTo", publishDateTo.Value.Date.AddDays(1).AddTicks(-1)));
        }

        if (createdFrom.HasValue)
        {
            where.Add("c.CreatedAt >= @CreatedFrom");
            parameters.Add(new SqlParameter("@CreatedFrom", createdFrom.Value));
        }

        if (createdTo.HasValue)
        {
            where.Add("c.CreatedAt <= @CreatedTo");
            parameters.Add(new SqlParameter("@CreatedTo", createdTo.Value.Date.AddDays(1).AddTicks(-1)));
        }

        if (hasFeaturedImage.HasValue)
        {
            where.Add(hasFeaturedImage.Value ? "NULLIF(c.FeaturedImage, '') IS NOT NULL" : "NULLIF(c.FeaturedImage, '') IS NULL");
        }

        if (!string.IsNullOrWhiteSpace(freeText))
        {
            where.Add(@"(
                c.Title LIKE @Search OR c.Title_En LIKE @Search OR
                c.Summary LIKE @Search OR c.Summary_En LIKE @Search OR
                c.Content LIKE @Search OR c.Content_En LIKE @Search OR
                c.SeoTitle LIKE @Search OR c.SeoTitle_En LIKE @Search OR
                c.SeoDescription LIKE @Search OR c.SeoDescription_En LIKE @Search OR
                c.SeoKeywords LIKE @Search OR c.SeoKeywords_En LIKE @Search OR
                c.CustomFieldsJson LIKE @Search OR
                ct.Name LIKE @Search OR ct.Name_En LIKE @Search OR
                cat.Name LIKE @Search OR cat.Name_En LIKE @Search OR
                sub.Name LIKE @Search OR sub.Name_En LIKE @Search
            )");
            parameters.Add(new SqlParameter("@Search", $"%{freeText.Trim()}%"));
        }

        AddCustomFieldFilters(where, parameters);

        string whereSql = string.Join(" AND ", where);
        int offset = (page - 1) * pageSize;
        var items = new List<object>();
        int total;
        string orderSql = BuildOrderSql(sortBy, sortDir);

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        string countSql = $@"
            SELECT COUNT(*)
            FROM dbo.Content c
            INNER JOIN dbo.ContentTypes ct ON c.ContentTypeId = ct.Id
            LEFT JOIN dbo.Categories cat ON c.CategoryId = cat.Id
            LEFT JOIN dbo.Categories sub ON c.SubCategoryId = sub.Id
            LEFT JOIN dbo.Users u ON c.CreatedBy = u.Id
            WHERE {whereSql}";

        await using (var countCommand = new SqlCommand(countSql, connection))
        {
            foreach (var parameter in parameters)
            {
                countCommand.Parameters.Add(new SqlParameter(parameter.ParameterName, parameter.Value));
            }

            total = Convert.ToInt32(await countCommand.ExecuteScalarAsync());
        }

        string query = $@"
            SELECT c.Id, c.ContentTypeId, c.Title, c.Title_En, c.Slug,
                   c.Summary, c.Summary_En, c.Content, c.Content_En, c.FeaturedImage,
                   c.Status, c.PublishDate, c.CategoryId, c.SubCategoryId,
                   c.SeoTitle, c.SeoTitle_En, c.SeoDescription, c.SeoDescription_En,
                   c.SeoKeywords, c.SeoKeywords_En, c.CanonicalUrl, c.CustomFieldsJson,
                   c.CreatedBy, c.CreatedAt, c.UpdatedBy, c.UpdatedAt,
                   ct.Name, ct.Name_En, ct.IconValue,
                   cat.Name, cat.Name_En, cat.Slug,
                   sub.Name, sub.Name_En, sub.Slug,
                   u.FullName, u.Username, c.Hits
            FROM dbo.Content c
            INNER JOIN dbo.ContentTypes ct ON c.ContentTypeId = ct.Id
            LEFT JOIN dbo.Categories cat ON c.CategoryId = cat.Id
            LEFT JOIN dbo.Categories sub ON c.SubCategoryId = sub.Id
            LEFT JOIN dbo.Users u ON c.CreatedBy = u.Id
            WHERE {whereSql}
            ORDER BY {orderSql}
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

        await using (var command = new SqlCommand(query, connection))
        {
            foreach (var parameter in parameters)
            {
                command.Parameters.Add(new SqlParameter(parameter.ParameterName, parameter.Value));
            }

            command.Parameters.AddWithValue("@Offset", offset);
            command.Parameters.AddWithValue("@PageSize", pageSize);

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var contentTypeIdValue = reader.GetInt64(1);
                var fields = ParseJsonObject(ReadString(reader, 21));
                // Security: strip sensitive fields before returning via API
                SanitizeFields(fields);
                var fieldsDisplay = await ResolveFieldsDisplayAsync(connectionString, contentTypeIdValue, fields);

                items.Add(new
                {
                    id = reader.GetInt64(0),
                    contentTypeId = contentTypeIdValue,
                    title = ReadString(reader, 2),
                    titleEn = ReadString(reader, 3),
                    slug = ReadString(reader, 4),
                    summary = ReadString(reader, 5),
                    summaryEn = ReadString(reader, 6),
                    content = ReadString(reader, 7),
                    contentEn = ReadString(reader, 8),
                    featuredImage = ReadString(reader, 9),
                    status = reader.GetString(10),
                    publishDate = ReadDate(reader, 11),
                    categoryId = ReadNullableInt64(reader, 12),
                    subCategoryId = ReadNullableInt64(reader, 13),
                    hits = reader.IsDBNull(37) ? 0 : reader.GetInt32(37),
                    hitts = reader.IsDBNull(37) ? 0 : reader.GetInt32(37),
                    seo = new
                    {
                        title = ReadString(reader, 14),
                        titleEn = ReadString(reader, 15),
                        description = ReadString(reader, 16),
                        descriptionEn = ReadString(reader, 17),
                        keywords = ReadString(reader, 18),
                        keywordsEn = ReadString(reader, 19),
                        canonical = ReadString(reader, 20)
                    },
                    fields,
                    fieldsDisplay,
                    createdBy = reader.GetInt64(22),
                    createdAt = reader.GetDateTime(23).ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    updatedBy = ReadNullableInt64(reader, 24),
                    updatedAt = ReadDate(reader, 25),
                    contentType = new
                    {
                        id = reader.GetInt64(1),
                        name = reader.GetString(26),
                        nameEn = ReadString(reader, 27),
                        icon = ReadString(reader, 28)
                    },
                    category = reader.IsDBNull(29) ? null : new
                    {
                        id = ReadNullableInt64(reader, 12),
                        name = reader.GetString(29),
                        nameEn = ReadString(reader, 30),
                        slug = ReadString(reader, 31)
                    },
                    subCategory = reader.IsDBNull(32) ? null : new
                    {
                        id = ReadNullableInt64(reader, 13),
                        name = reader.GetString(32),
                        nameEn = ReadString(reader, 33),
                        slug = ReadString(reader, 34)
                    },
                    author = new
                    {
                        id = reader.GetInt64(22),
                        name = ReadString(reader, 35) ?? ReadString(reader, 36) ?? "System",
                        username = ReadString(reader, 36)
                    }
                });
            }
        }

        return Ok(new
        {
            data = items,
            meta = new
            {
                pagination = new
                {
                    page,
                    pageSize,
                    total,
                    totalPages = (int)Math.Ceiling(total / (double)pageSize),
                    hasNext = page * pageSize < total,
                    hasPrevious = page > 1
                },
                filters = new
                {
                    q = freeText,
                    contentTypeId,
                    contentType,
                    categoryId,
                    subCategoryId,
                    status,
                    slug,
                    authorId,
                    publishDateFrom,
                    publishDateTo,
                    createdFrom,
                    createdTo,
                    hasFeaturedImage,
                    sortBy,
                    sortDir
                }
            }
        });
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id)
    {
        string? connectionString = _configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return Problem("Database connection is not configured.");
        }

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        const string query = @"
            SELECT c.Id, c.ContentTypeId, c.Title, c.Title_En, c.Slug, c.Summary, c.Summary_En, c.Content, c.Content_En,
                   c.FeaturedImage, c.Status, c.PublishDate, c.SeoTitle, c.SeoTitle_En,
                   c.SeoDescription, c.SeoDescription_En, c.SeoKeywords, c.SeoKeywords_En,
                   c.CanonicalUrl, c.CustomFieldsJson, c.CreatedAt, ct.Name, ct.Name_En,
                   cat.Name, cat.Name_En, sub.Name, sub.Name_En, c.Hits
            FROM dbo.Content c
            INNER JOIN dbo.ContentTypes ct ON c.ContentTypeId = ct.Id
            LEFT JOIN dbo.Categories cat ON c.CategoryId = cat.Id
            LEFT JOIN dbo.Categories sub ON c.SubCategoryId = sub.Id
            WHERE c.Id = @Id AND c.IsDeleted = 0";

        await using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@Id", id);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return NotFound(new { message = "Content was not found." });
        }

        var detailFields = ParseJsonObject(ReadString(reader, 19));
        // Security: strip sensitive fields before returning via API
        SanitizeFields(detailFields);
        var detailFieldsDisplay = await ResolveFieldsDisplayAsync(connectionString, reader.GetInt64(1), detailFields);

        return Ok(new
        {
            id = reader.GetInt64(0),
            contentTypeId = reader.GetInt64(1),
            title = ReadString(reader, 2),
            titleEn = ReadString(reader, 3),
            slug = ReadString(reader, 4),
            summary = ReadString(reader, 5),
            summaryEn = ReadString(reader, 6),
            content = ReadString(reader, 7),
            contentEn = ReadString(reader, 8),
            featuredImage = ReadString(reader, 9),
            status = reader.GetString(10),
            publishDate = ReadDate(reader, 11),
            hits = reader.IsDBNull(27) ? 0 : reader.GetInt32(27),
            hitts = reader.IsDBNull(27) ? 0 : reader.GetInt32(27),
            seo = new
            {
                title = ReadString(reader, 12),
                titleEn = ReadString(reader, 13),
                description = ReadString(reader, 14),
                descriptionEn = ReadString(reader, 15),
                keywords = ReadString(reader, 16),
                keywordsEn = ReadString(reader, 17),
                canonical = ReadString(reader, 18)
            },
            fields = detailFields,
            fieldsDisplay = detailFieldsDisplay,
            createdAt = reader.GetDateTime(20).ToString("yyyy-MM-ddTHH:mm:ssZ"),
            contentType = new
            {
                id = reader.GetInt64(1),
                name = reader.GetString(21),
                nameEn = ReadString(reader, 22)
            },
            category = reader.IsDBNull(23) ? null : new
            {
                name = reader.GetString(23),
                nameEn = ReadString(reader, 24)
            },
            subCategory = reader.IsDBNull(25) ? null : new
            {
                name = reader.GetString(25),
                nameEn = ReadString(reader, 26)
            }
        });
    }

    private static string? ReadString(SqlDataReader reader, int index) => reader.IsDBNull(index) ? null : reader.GetString(index);

    private static string? ReadDate(SqlDataReader reader, int index) => reader.IsDBNull(index) ? null : reader.GetDateTime(index).ToString("yyyy-MM-ddTHH:mm:ssZ");

    private static long? ReadNullableInt64(SqlDataReader reader, int index) => reader.IsDBNull(index) ? null : reader.GetInt64(index);

    private void AddCustomFieldFilters(List<string> where, List<SqlParameter> parameters)
    {
        int index = 0;
        foreach (var queryItem in Request.Query)
        {
            string key = queryItem.Key;
            if (!key.StartsWith("field.", StringComparison.OrdinalIgnoreCase) && !key.StartsWith("fields.", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string fieldName = key[(key.IndexOf('.') + 1)..];
            string? fieldValue = queryItem.Value.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(fieldName) || string.IsNullOrWhiteSpace(fieldValue))
            {
                continue;
            }

            if (!Regex.IsMatch(fieldName, "^[A-Za-z0-9_]+$"))
            {
                continue;
            }

            string parameterName = $"@CustomField{index++}";
            where.Add($"JSON_VALUE(c.CustomFieldsJson, '$.\"{fieldName}\"') LIKE {parameterName}");
            parameters.Add(new SqlParameter(parameterName, $"%{fieldValue.Trim()}%"));
        }
    }

    private static string BuildOrderSql(string? sortBy, string? sortDir)
    {
        string direction = string.Equals(sortDir, "asc", StringComparison.OrdinalIgnoreCase) ? "ASC" : "DESC";
        string column = sortBy?.Trim().ToLowerInvariant() switch
        {
            "id" => "c.Id",
            "title" => "c.Title",
            "slug" => "c.Slug",
            "status" => "c.Status",
            "contenttype" => "ct.Name_En",
            "category" => "cat.Name",
            "subcategory" => "sub.Name",
            "createdat" => "c.CreatedAt",
            "updatedat" => "c.UpdatedAt",
            "publishdate" => "c.PublishDate",
            _ => "COALESCE(c.PublishDate, c.CreatedAt)"
        };

        return $"{column} {direction}, c.Id DESC";
    }

    private static Dictionary<string, object> ParseJsonObject(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, object>();
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object>>(json) ?? new Dictionary<string, object>();
        }
        catch
        {
            return new Dictionary<string, object>();
        }
    }

    private static async Task<Dictionary<string, object>> ResolveFieldsDisplayAsync(string connectionString, long contentTypeId, Dictionary<string, object> rawFields)
    {
        var result = new Dictionary<string, object>();
        if (rawFields.Count == 0)
        {
            return result;
        }

        var optionFields = new Dictionary<string, List<ApiSelectOption>>(StringComparer.OrdinalIgnoreCase);
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        const string query = @"
            SELECT FieldName, OptionsJson
            FROM dbo.ContentTypeFields
            WHERE ContentTypeId = @ContentTypeId
              AND FieldType IN ('Select', 'MultiSelect')
              AND OptionsJson IS NOT NULL";

        await using (var command = new SqlCommand(query, connection))
        {
            command.Parameters.AddWithValue("@ContentTypeId", contentTypeId);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                optionFields[reader.GetString(0)] = await ResolveOptionsJsonAsync(connectionString, reader.GetString(1));
            }
        }

        foreach (var field in rawFields)
        {
            result[field.Key] = optionFields.TryGetValue(field.Key, out var options)
                ? ResolveOptionDisplayValue(field.Value, options)
                : field.Value;
        }

        return result;
    }

    private static async Task<List<ApiSelectOption>> ResolveOptionsJsonAsync(string connectionString, string optionsJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(optionsJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                return JsonSerializer.Deserialize<List<ApiSelectOption>>(optionsJson) ?? new();
            }

            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("mode", out var modeElement) &&
                string.Equals(modeElement.GetString(), "query", StringComparison.OrdinalIgnoreCase) &&
                doc.RootElement.TryGetProperty("options", out var queryOptionsElement))
            {
                return JsonSerializer.Deserialize<List<ApiSelectOption>>(queryOptionsElement.GetRawText()) ?? new();
            }

            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("mode", out modeElement) &&
                string.Equals(modeElement.GetString(), "dynamic", StringComparison.OrdinalIgnoreCase))
            {
                string source = doc.RootElement.TryGetProperty("source", out var sourceElement) ? sourceElement.GetString() ?? string.Empty : string.Empty;
                long sourceId = doc.RootElement.TryGetProperty("sourceId", out var sourceIdElement) ? sourceIdElement.GetInt64() : 0;
                return await LoadDynamicOptionsAsync(connectionString, source, sourceId);
            }
        }
        catch
        {
        }

        return new();
    }

    private static async Task<List<ApiSelectOption>> LoadDynamicOptionsAsync(string connectionString, string source, long sourceId)
    {
        var options = new List<ApiSelectOption>();
        if (sourceId <= 0)
        {
            return options;
        }

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        string query = source.Equals("user", StringComparison.OrdinalIgnoreCase)
            ? @"
                SELECT u.Id, u.FullName, u.Username
                FROM dbo.UserRoles ur
                INNER JOIN dbo.Users u ON ur.UserId = u.Id
                WHERE ur.RoleId = @SourceId AND u.IsActive = 1
                ORDER BY u.FullName, u.Username"
            : @"
                SELECT Id, Name, Name_En
                FROM dbo.Categories
                WHERE ParentId = @SourceId AND IsActive = 1
                ORDER BY SortOrder ASC, Name ASC";

        await using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@SourceId", sourceId);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (source.Equals("user", StringComparison.OrdinalIgnoreCase))
            {
                var username = reader.GetString(2);
                var fullName = reader.IsDBNull(1) ? username : reader.GetString(1);
                options.Add(new ApiSelectOption { value = reader.GetInt64(0).ToString(), label = fullName, labelEn = fullName });
            }
            else
            {
                var label = reader.GetString(1);
                options.Add(new ApiSelectOption { value = reader.GetInt64(0).ToString(), label = label, labelEn = reader.IsDBNull(2) ? label : reader.GetString(2) });
            }
        }

        return options;
    }

    private static object ResolveOptionDisplayValue(object value, List<ApiSelectOption> options)
    {
        if (value is JsonElement element && element.ValueKind == JsonValueKind.Array)
        {
            return element.EnumerateArray().Select(e => ResolveSingleOption(ReadJsonElementAsString(e) ?? string.Empty, options)).ToList();
        }

        if (value is JsonElement scalarElement)
        {
            return ResolveSingleOption(ReadJsonElementAsString(scalarElement) ?? string.Empty, options);
        }

        return ResolveSingleOption(value?.ToString() ?? string.Empty, options);
    }

    private static object ResolveSingleOption(string value, List<ApiSelectOption> options)
    {
        var option = options.FirstOrDefault(o => string.Equals(o.value, value, StringComparison.OrdinalIgnoreCase));
        return new { value, label = option?.label ?? value, labelEn = option?.labelEn ?? option?.label ?? value };
    }

    private static string? ReadJsonElementAsString(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => element.ToString()
        };
    }

    private class ApiSelectOption
    {
        public string value { get; set; } = string.Empty;
        public string label { get; set; } = string.Empty;
        public string? labelEn { get; set; }
    }

    /// <summary>
    /// Removes keys that correspond to sensitive database columns (passwords, tokens, secrets)
    /// from a dictionary before it is returned in any API response.
    /// </summary>
    private static void SanitizeFields(Dictionary<string, object> fields)
    {
        // Sensitive column names — mirrors ContentTypesModel.SensitiveColumns
        var sensitiveKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "PasswordHash", "Password", "PasswordSalt",
            "Token", "AccessToken", "RefreshToken", "ResetToken", "ConfirmToken",
            "Secret", "ApiSecret", "PrivateKey", "ClientSecret",
            "Pin", "Otp", "TwoFactorSecret",
            "SecurityStamp", "ConcurrencyStamp"
        };

        foreach (var key in fields.Keys.Where(k => sensitiveKeys.Contains(k)).ToList())
        {
            fields.Remove(key);
        }
    }
}
