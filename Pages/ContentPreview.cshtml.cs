using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace TalaPress.Pages
{
    public class ContentPreviewModel : PageModel
    {
        private readonly IConfiguration _configuration;

        public ContentPreviewModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // Content Model Properties
        public long Id { get; set; }
        public long ContentTypeId { get; set; }
        public string ContentTypeName { get; set; } = string.Empty;
        public string ContentTypeNameEn { get; set; } = string.Empty;
        public string? Title { get; set; }
        public string? Title_En { get; set; }
        public string? Slug { get; set; }
        public string? Summary { get; set; }
        public string? Summary_En { get; set; }
        public string? Content { get; set; }
        public string? Content_En { get; set; }
        public string? FeaturedImage { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime? PublishDate { get; set; }
        public string? CategoryName { get; set; }
        public string? CategoryNameEn { get; set; }
        public string? SubCategoryName { get; set; }
        public string? SubCategoryNameEn { get; set; }
        public string? SeoTitle { get; set; }
        public string? SeoTitle_En { get; set; }
        public string? SeoDescription { get; set; }
        public string? SeoDescription_En { get; set; }
        public string? SeoKeywords { get; set; }
        public string? SeoKeywords_En { get; set; }
        public string? CanonicalUrl { get; set; }
        public string CustomFieldsJson { get; set; } = "{}";
        public string AuthorName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public int Hits { get; set; }

        public List<CustomFieldDto> ContentFieldsList { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(long id)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return RedirectToPage("/Login");
            }

            string? connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                TempData["ErrorMessage"] = "جملة الاتصال بقاعدة البيانات غير مهيأة.";
                return RedirectToPage("/Content");
            }

            Id = id;
            bool found = await LoadPreviewDataAsync(connectionString, id);
            if (!found)
            {
                TempData["ErrorMessage"] = "تعذر العثور على المحتوى المطلوب لمعاينته.";
                return RedirectToPage("/Content");
            }

            // Load Custom Fields Schema Definitions
            ContentFieldsList = await GetCustomFieldsListAsync(connectionString, ContentTypeId);

            return Page();
        }

        // Endpoint: /ContentPreview?id={id}&handler=JSON
        public async Task<IActionResult> OnGetJSONAsync(long id)
        {
            IActionResult? denied = RequireContentViewJsonAccess();
            if (denied != null)
            {
                return denied;
            }

            string? connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                return new JsonResult(new { error = "Connection string is not configured." });
            }

            bool found = await LoadPreviewDataAsync(connectionString, id);
            if (!found)
            {
                return new JsonResult(new { error = $"Content with ID {id} not found." });
            }

            // Deserialize dynamic fields from JSON string
            var fieldsDict = new Dictionary<string, object>();
            try
            {
                if (!string.IsNullOrEmpty(CustomFieldsJson))
                {
                    fieldsDict = JsonSerializer.Deserialize<Dictionary<string, object>>(CustomFieldsJson) ?? new();
                }
            }
            catch
            {
                // Fallback on json parsing failure
            }

            // Security: strip sensitive keys from custom fields before exposing via API
            SanitizeFields(fieldsDict);

            var fieldsDisplay = await ResolveFieldsDisplayAsync(connectionString, ContentTypeId, fieldsDict);

            // Build headless API response payload matching the required format
            var payload = new
            {
                id = Id,
                contentType = ContentTypeNameEn,
                contentTypeAr = ContentTypeName,
                title = Title,
                titleEn = Title_En,
                slug = Slug,
                summary = Summary,
                summaryEn = Summary_En,
                content = Content,
                contentEn = Content_En,
                featuredImage = FeaturedImage,
                status = Status,
                publishDate = PublishDate?.ToString("yyyy-MM-dd HH:mm:ss"),
                category = CategoryName,
                categoryEn = CategoryNameEn,
                subCategory = SubCategoryName,
                subCategoryEn = SubCategoryNameEn,
                seo = new
                {
                    title = SeoTitle,
                    titleEn = SeoTitle_En,
                    description = SeoDescription,
                    descriptionEn = SeoDescription_En,
                    keywords = SeoKeywords,
                    keywordsEn = SeoKeywords_En,
                    canonical = CanonicalUrl
                },
                fields = fieldsDict,
                fieldsDisplay,
                author = AuthorName,
                hits = Hits,
                hitts = Hits,
                createdAt = CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
            };

            return new JsonResult(payload);
        }

        private async Task<bool> LoadPreviewDataAsync(string connectionString, long id)
        {
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            string query = @"
                SELECT c.Title, c.Title_En, c.Slug, c.Summary, c.Summary_En, c.Content, c.Content_En, 
                       c.FeaturedImage, c.Status, c.PublishDate, c.SeoTitle, c.SeoTitle_En, 
                       c.SeoDescription, c.SeoDescription_En, c.SeoKeywords, c.SeoKeywords_En, 
                       c.CanonicalUrl, c.CustomFieldsJson, c.CreatedAt,
                       ct.Name AS ContentTypeName, ct.Name_En AS ContentTypeNameEn,
                       cat.Name AS CategoryName, cat.Name_En AS CategoryNameEn,
                       sub.Name AS SubCategoryName, sub.Name_En AS SubCategoryNameEn,
                       u.FullName AS AuthorName, c.ContentTypeId, c.Hits
                FROM dbo.Content c
                INNER JOIN dbo.ContentTypes ct ON c.ContentTypeId = ct.Id
                LEFT JOIN dbo.Categories cat ON c.CategoryId = cat.Id
                LEFT JOIN dbo.Categories sub ON c.SubCategoryId = sub.Id
                LEFT JOIN dbo.Users u ON c.CreatedBy = u.Id
                WHERE c.Id = @Id AND c.IsDeleted = 0";

            using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@Id", id);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                Title = reader.IsDBNull(0) ? null : reader.GetString(0);
                Title_En = reader.IsDBNull(1) ? null : reader.GetString(1);
                Slug = reader.IsDBNull(2) ? null : reader.GetString(2);
                Summary = reader.IsDBNull(3) ? null : reader.GetString(3);
                Summary_En = reader.IsDBNull(4) ? null : reader.GetString(4);
                Content = reader.IsDBNull(5) ? null : reader.GetString(5);
                Content_En = reader.IsDBNull(6) ? null : reader.GetString(6);
                FeaturedImage = reader.IsDBNull(7) ? null : reader.GetString(7);
                Status = reader.GetString(8);
                PublishDate = reader.IsDBNull(9) ? null : reader.GetDateTime(9);
                SeoTitle = reader.IsDBNull(10) ? null : reader.GetString(10);
                SeoTitle_En = reader.IsDBNull(11) ? null : reader.GetString(11);
                SeoDescription = reader.IsDBNull(12) ? null : reader.GetString(12);
                SeoDescription_En = reader.IsDBNull(13) ? null : reader.GetString(13);
                SeoKeywords = reader.IsDBNull(14) ? null : reader.GetString(14);
                SeoKeywords_En = reader.IsDBNull(15) ? null : reader.GetString(15);
                CanonicalUrl = reader.IsDBNull(16) ? null : reader.GetString(16);
                CustomFieldsJson = reader.IsDBNull(17) ? "{}" : reader.GetString(17);
                CreatedAt = reader.GetDateTime(18);
                ContentTypeName = reader.GetString(19);
                ContentTypeNameEn = reader.IsDBNull(20) ? reader.GetString(19) : reader.GetString(20);
                CategoryName = reader.IsDBNull(21) ? null : reader.GetString(21);
                CategoryNameEn = reader.IsDBNull(22) ? null : reader.GetString(22);
                SubCategoryName = reader.IsDBNull(23) ? null : reader.GetString(23);
                SubCategoryNameEn = reader.IsDBNull(24) ? null : reader.GetString(24);
                AuthorName = reader.IsDBNull(25) ? "System" : reader.GetString(25);
                ContentTypeId = reader.GetInt64(26);
                Hits = reader.IsDBNull(27) ? 0 : reader.GetInt32(27);
                return true;
            }

            return false;
        }

        private async Task<List<CustomFieldDto>> GetCustomFieldsListAsync(string connectionString, long typeId)
        {
            var dbFields = new List<CustomFieldDto>();
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            string query = @"
                SELECT Id, FieldName, Label, Label_En, FieldType, Placeholder, Placeholder_En, HelpText, HelpText_En, 
                       IsRequired, IsTranslatable, DefaultValue, OptionsJson, SortOrder, IsActive 
                FROM dbo.ContentTypeFields 
                WHERE ContentTypeId = @ContentTypeId AND IsActive = 1
                ORDER BY SortOrder ASC";

            using (var command = new SqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@ContentTypeId", typeId);
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    dbFields.Add(new CustomFieldDto
                    {
                        Id = reader.GetInt64(0),
                        FieldName = reader.GetString(1),
                        Label = reader.GetString(2),
                        Label_En = reader.IsDBNull(3) ? null : reader.GetString(3),
                        FieldType = reader.GetString(4),
                        Placeholder = reader.IsDBNull(5) ? null : reader.GetString(5),
                        Placeholder_En = reader.IsDBNull(6) ? null : reader.GetString(6),
                        HelpText = reader.IsDBNull(7) ? null : reader.GetString(7),
                        HelpText_En = reader.IsDBNull(8) ? null : reader.GetString(8),
                        IsRequired = reader.GetBoolean(9),
                        IsTranslatable = reader.GetBoolean(10),
                        DefaultValue = reader.IsDBNull(11) ? null : reader.GetString(11),
                        OptionsJson = reader.IsDBNull(12) ? null : reader.GetString(12),
                        SortOrder = reader.GetInt32(13),
                        IsActive = reader.GetBoolean(14),
                        IsSystemField = ContentTypesModel.PredefinedFields.Any(f => f.FieldName.Equals(reader.GetString(1), StringComparison.OrdinalIgnoreCase))
                    });
                }
            }

            return dbFields;
        }

        private static async Task<Dictionary<string, object>> ResolveFieldsDisplayAsync(string connectionString, long contentTypeId, Dictionary<string, object> rawFields)
        {
            var result = new Dictionary<string, object>();
            if (rawFields.Count == 0)
            {
                return result;
            }

            var optionFields = new Dictionary<string, List<SelectOptionDto>>(StringComparer.OrdinalIgnoreCase);

            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            const string query = @"
                SELECT FieldName, OptionsJson
                FROM dbo.ContentTypeFields
                WHERE ContentTypeId = @ContentTypeId
                  AND FieldType IN ('Select', 'MultiSelect')
                  AND OptionsJson IS NOT NULL";

            using (var command = new SqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@ContentTypeId", contentTypeId);
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var fieldName = reader.GetString(0);
                    var optionsJson = reader.GetString(1);
                    var options = await ResolveOptionsJsonAsync(connectionString, optionsJson);
                    optionFields[fieldName] = options;
                }
            }

            foreach (var field in rawFields)
            {
                if (!optionFields.TryGetValue(field.Key, out var options))
                {
                    result[field.Key] = field.Value;
                    continue;
                }

                result[field.Key] = ResolveOptionDisplayValue(field.Value, options);
            }

            return result;
        }

        private static async Task<List<SelectOptionDto>> ResolveOptionsJsonAsync(string connectionString, string optionsJson)
        {
            try
            {
                using var doc = JsonDocument.Parse(optionsJson);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    return JsonSerializer.Deserialize<List<SelectOptionDto>>(optionsJson) ?? new();
                }

                if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                    doc.RootElement.TryGetProperty("mode", out var modeElement) &&
                    string.Equals(modeElement.GetString(), "query", StringComparison.OrdinalIgnoreCase) &&
                    doc.RootElement.TryGetProperty("options", out var queryOptionsElement))
                {
                    return JsonSerializer.Deserialize<List<SelectOptionDto>>(queryOptionsElement.GetRawText()) ?? new();
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

        private static async Task<List<SelectOptionDto>> LoadDynamicOptionsAsync(string connectionString, string source, long sourceId)
        {
            var options = new List<SelectOptionDto>();
            if (sourceId <= 0)
            {
                return options;
            }

            using var connection = new SqlConnection(connectionString);
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

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@SourceId", sourceId);
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (source.Equals("user", StringComparison.OrdinalIgnoreCase))
                {
                    var username = reader.GetString(2);
                    var fullName = reader.IsDBNull(1) ? username : reader.GetString(1);
                    options.Add(new SelectOptionDto { value = reader.GetInt64(0).ToString(), label = fullName, labelEn = fullName });
                }
                else
                {
                    var label = reader.GetString(1);
                    options.Add(new SelectOptionDto { value = reader.GetInt64(0).ToString(), label = label, labelEn = reader.IsDBNull(2) ? label : reader.GetString(2) });
                }
            }

            return options;
        }

        private static object ResolveOptionDisplayValue(object value, List<SelectOptionDto> options)
        {
            if (value is JsonElement element)
            {
                if (element.ValueKind == JsonValueKind.Array)
                {
                    var selectedValues = element.EnumerateArray().Select(ReadJsonElementAsString).Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
                    return selectedValues.Select(v => ResolveSingleOption(v!, options)).ToList();
                }

                return ResolveSingleOption(ReadJsonElementAsString(element) ?? string.Empty, options);
            }

            if (value is IEnumerable<object> optionValues && value is not string)
            {
                return optionValues.Select(v => ResolveSingleOption(v?.ToString() ?? string.Empty, options)).ToList();
            }

            return ResolveSingleOption(value?.ToString() ?? string.Empty, options);
        }

        private static object ResolveSingleOption(string value, List<SelectOptionDto> options)
        {
            var option = options.FirstOrDefault(o => string.Equals(o.value, value, StringComparison.OrdinalIgnoreCase));
            return new
            {
                value,
                label = option?.label ?? value,
                labelEn = option?.labelEn ?? option?.label ?? value
            };
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

        /// <summary>
        /// Removes keys that correspond to sensitive database columns (passwords, tokens, secrets)
        /// from a dictionary before it is returned in any API or JSON response.
        /// </summary>
        private static void SanitizeFields(Dictionary<string, object> fields)
        {
            // Use the shared blocklist defined in ContentTypesModel
            foreach (var key in fields.Keys
                .Where(k => ContentTypesModel.SensitiveColumns.Contains(k))
                .ToList())
            {
                fields.Remove(key);
            }
        }

        private IActionResult? RequireContentViewJsonAccess()
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return new JsonResult(new { error = "Unauthorized." }) { StatusCode = StatusCodes.Status401Unauthorized };
            }

            if (!User.HasClaim("Permission", "Content.View"))
            {
                return new JsonResult(new { error = "Forbidden." }) { StatusCode = StatusCodes.Status403Forbidden };
            }

            return null;
        }
    }
}
