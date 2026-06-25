using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using System.IO;
using TalaPress.Infrastructure;

namespace TalaPress.Pages
{
    public class ContentEditModel : PageModel
    {
        private readonly IConfiguration _configuration;

        public ContentEditModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // Form Bindings
        [BindProperty]
        public long Id { get; set; }

        [BindProperty]
        public long ContentTypeId { get; set; }

        [BindProperty]
        public string? Title { get; set; }

        [BindProperty]
        public string? Title_En { get; set; }

        [BindProperty]
        public string? Slug { get; set; }

        [BindProperty]
        public string? Summary { get; set; }

        [BindProperty]
        public string? Summary_En { get; set; }

        [BindProperty]
        public string? Content { get; set; }

        [BindProperty]
        public string? Content_En { get; set; }

        [BindProperty]
        public string? FeaturedImage { get; set; }

        [BindProperty]
        public string Status { get; set; } = "Draft";

        [BindProperty]
        public DateTime? PublishDate { get; set; }

        [BindProperty]
        public long? CategoryId { get; set; }

        [BindProperty]
        public long? SubCategoryId { get; set; }

        [BindProperty]
        public int Hits { get; set; }

        [BindProperty]
        public string? SeoTitle { get; set; }

        [BindProperty]
        public string? SeoTitle_En { get; set; }

        [BindProperty]
        public string? SeoDescription { get; set; }

        [BindProperty]
        public string? SeoDescription_En { get; set; }

        [BindProperty]
        public string? SeoKeywords { get; set; }

        [BindProperty]
        public string? SeoKeywords_En { get; set; }

        [BindProperty]
        public string? CanonicalUrl { get; set; }

        [BindProperty]
        public string CustomFieldsValuesJson { get; set; } = "{}";

        private Dictionary<string, JsonElement> _parsedCustomFields = new();

        public string? GetCustomFieldString(string fieldName)
        {
            if (_parsedCustomFields.TryGetValue(fieldName, out var element))
            {
                return element.ValueKind switch
                {
                    JsonValueKind.String => element.GetString(),
                    JsonValueKind.Number => element.GetRawText(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    _ => element.GetRawText()
                };
            }

            return null;
        }

        public bool GetCustomFieldBool(string fieldName)
        {
            if (_parsedCustomFields.TryGetValue(fieldName, out var element))
            {
                return element.ValueKind switch
                {
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.String => bool.TryParse(element.GetString(), out var parsed) && parsed,
                    _ => false
                };
            }

            return false;
        }

        private void ParseCustomFieldValues()
        {
            try
            {
                _parsedCustomFields = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(CustomFieldsValuesJson)
                    ?? new Dictionary<string, JsonElement>();
            }
            catch
            {
                _parsedCustomFields = new Dictionary<string, JsonElement>();
            }
        }

        // View Metadata Lists
        public string ContentTypeName { get; set; } = string.Empty;
        public string ContentTypeNameEn { get; set; } = string.Empty;
        public List<ContentTypeFilterDto> AllContentTypes { get; set; } = new();
        public List<CategoryFilterDto> CategoriesList { get; set; } = new();
        public List<CustomFieldDto> ContentFieldsList { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(long? id, long? contentTypeId)
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

            // Load selection lists
            await LoadMetadataListsAsync(connectionString);

            if (id.HasValue && id.Value > 0)
            {
                // EDIT MODE
                if (!User.HasClaim("Permission", "Content.Edit"))
                {
                    TempData["ErrorMessage"] = "ليس لديك صلاحية كافية لتعديل المحتوى.";
                    return RedirectToPage("/Content");
                }

                Id = id.Value;
                bool found = await LoadContentRowAsync(connectionString, Id);
                if (!found)
                {
                    TempData["ErrorMessage"] = "تعذر العثور على المنشور المحدد.";
                    return RedirectToPage("/Content");
                }
            }
            else
            {
                // CREATE MODE
                if (!User.HasClaim("Permission", "Content.Create"))
                {
                    TempData["ErrorMessage"] = "ليس لديك صلاحية كافية لإنشاء محتوى جديد.";
                    return RedirectToPage("/Content");
                }

                Id = 0;
                if (contentTypeId.HasValue && contentTypeId.Value > 0)
                {
                    ContentTypeId = contentTypeId.Value;
                }
                else
                {
                    // Fallback to first available content type
                    var firstType = AllContentTypes.FirstOrDefault();
                    if (firstType != null)
                    {
                        ContentTypeId = firstType.Id;
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "يرجى إنشاء وتهيئة أنواع المحتوى أولاً.";
                        return RedirectToPage("/ContentTypes");
                    }
                }
            }

            // Load dynamic custom fields layout configuration for current type
            var currentType = AllContentTypes.FirstOrDefault(t => t.Id == ContentTypeId);
            if (currentType != null)
            {
                ContentTypeName = currentType.Name;
                ContentTypeNameEn = currentType.Name_En ?? currentType.Name;
            }

            ApplyCategoryScopeForContentType();

            ContentFieldsList = await GetCustomFieldsListAsync(connectionString, ContentTypeId);
            ParseCustomFieldValues();
            return Page();
        }

        // =========================================================================
        // AJAX Save Handler (Create / Update)
        // =========================================================================
        public async Task<IActionResult> OnPostSaveAsync()
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return new JsonResult(new { success = false, message = "غير مصرح بالدخول." });
            }

            bool isNew = Id == 0;
            string requiredPermission = isNew ? "Content.Create" : "Content.Edit";
            if (!User.HasClaim("Permission", requiredPermission))
            {
                return new JsonResult(new { success = false, message = "ليس لديك صلاحية كافية لإجراء هذا التعديل." });
            }

            string? connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                return new JsonResult(new { success = false, message = "خطأ بتهيئة قاعدة البيانات." });
            }

            // 1. Basic Validations
            if (string.IsNullOrWhiteSpace(Title) && string.IsNullOrWhiteSpace(Title_En))
            {
                return new JsonResult(new { success = false, message = "يجب إدخال عنوان للمنشور." });
            }

            // Sync multilingual titles/summaries/contents if one is blank
            if (string.IsNullOrWhiteSpace(Title)) Title = Title_En;
            if (string.IsNullOrWhiteSpace(Title_En)) Title_En = Title;
            if (string.IsNullOrWhiteSpace(Summary)) Summary = Summary_En;
            if (string.IsNullOrWhiteSpace(Summary_En)) Summary_En = Summary;
            if (string.IsNullOrWhiteSpace(Content)) Content = Content_En;
            if (string.IsNullOrWhiteSpace(Content_En)) Content_En = Content;
            if (string.IsNullOrWhiteSpace(SeoTitle)) SeoTitle = SeoTitle_En;
            if (string.IsNullOrWhiteSpace(SeoTitle_En)) SeoTitle_En = SeoTitle;
            if (string.IsNullOrWhiteSpace(SeoDescription)) SeoDescription = SeoDescription_En;
            if (string.IsNullOrWhiteSpace(SeoDescription_En)) SeoDescription_En = SeoDescription;
            if (string.IsNullOrWhiteSpace(SeoKeywords)) SeoKeywords = SeoKeywords_En;
            if (string.IsNullOrWhiteSpace(SeoKeywords_En)) SeoKeywords_En = SeoKeywords;

            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            long currentUserId = userIdClaim != null ? long.Parse(userIdClaim.Value) : 0;

            try
            {
                // 2. Validate and collect dynamic custom fields
                var fields = await GetCustomFieldsListAsync(connectionString, ContentTypeId);
                var customFieldsValues = new Dictionary<string, object>();

                if (!isNew)
                {
                    await using (var loadConnection = new SqlConnection(connectionString))
                    {
                        await loadConnection.OpenAsync();
                        using var loadCmd = new SqlCommand(
                            "SELECT CustomFieldsJson FROM dbo.Content WHERE Id = @Id AND IsDeleted = 0",
                            loadConnection);
                        loadCmd.Parameters.AddWithValue("@Id", Id);
                        var existingJson = await loadCmd.ExecuteScalarAsync() as string;
                        CustomFieldsValuesJson = string.IsNullOrWhiteSpace(existingJson) ? "{}" : existingJson;
                    }

                    ParseCustomFieldValues();
                    foreach (var existing in _parsedCustomFields)
                    {
                        customFieldsValues[existing.Key] = JsonElementToObject(existing.Value);
                    }
                }

                foreach (var field in fields.Where(f => !f.IsSystemField && f.IsActive))
                {
                    string formKey = $"custom_{field.FieldName}";

                    if (field.FieldType == "MultiSelect")
                    {
                        if (Request.Form.ContainsKey(formKey))
                        {
                            var listValues = Request.Form[formKey].ToList();
                            customFieldsValues[field.FieldName] = listValues;
                        }
                        continue;
                    }

                    if (field.FieldType == "Boolean")
                    {
                        customFieldsValues[field.FieldName] = Request.Form.ContainsKey(formKey);
                        continue;
                    }

                    if (!Request.Form.ContainsKey(formKey))
                    {
                        continue;
                    }

                    string? value = Request.Form[formKey];

                    // Check validation rules
                    if (field.IsRequired && string.IsNullOrWhiteSpace(value))
                    {
                        string fieldNameText = CultureInfoCurrent() == "ar" ? field.Label : (field.Label_En ?? field.Label);
                        return new JsonResult(new { success = false, message = $"الحقل '{fieldNameText}' مطلوب إجباري." });
                    }

                    if (value != null)
                    {
                        customFieldsValues[field.FieldName] = value;
                    }
                }

                string customFieldsJsonString = JsonSerializer.Serialize(customFieldsValues);

                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();
            await EnsureCategoryContentTypeColumnAsync(connection);

                if (isNew)
                {
                    // INSERT
                    string insertQuery = @"
                        INSERT INTO dbo.Content 
                        (ContentTypeId, Title, Title_En, Slug, Summary, Summary_En, Content, Content_En, 
                         FeaturedImage, Status, PublishDate, CategoryId, SubCategoryId, SeoTitle, SeoTitle_En, 
                         SeoDescription, SeoDescription_En, SeoKeywords, SeoKeywords_En, CanonicalUrl, 
                         CustomFieldsJson, CreatedBy, CreatedAt, IsDeleted, Hits)
                        VALUES 
                        (@ContentTypeId, @Title, @Title_En, @Slug, @Summary, @Summary_En, @Content, @Content_En, 
                         @FeaturedImage, @Status, @PublishDate, @CategoryId, @SubCategoryId, @SeoTitle, @SeoTitle_En, 
                         @SeoDescription, @SeoDescription_En, @SeoKeywords, @SeoKeywords_En, @CanonicalUrl, 
                         @CustomFieldsJson, @CreatedBy, GETUTCDATE(), 0, @Hits)";

                    using var cmd = new SqlCommand(insertQuery, connection);
                    cmd.Parameters.AddWithValue("@ContentTypeId", ContentTypeId);
                    cmd.Parameters.AddWithValue("@Title", (object?)Title ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Title_En", (object?)Title_En ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Slug", (object?)Slug ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Summary", (object?)Summary ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Summary_En", (object?)Summary_En ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Content", (object?)Content ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Content_En", (object?)Content_En ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@FeaturedImage", (object?)FeaturedImage ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Status", Status);
                    cmd.Parameters.AddWithValue("@PublishDate", (object?)PublishDate ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@CategoryId", (object?)CategoryId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@SubCategoryId", (object?)SubCategoryId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@SeoTitle", (object?)SeoTitle ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@SeoTitle_En", (object?)SeoTitle_En ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@SeoDescription", (object?)SeoDescription ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@SeoDescription_En", (object?)SeoDescription_En ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@SeoKeywords", (object?)SeoKeywords ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@SeoKeywords_En", (object?)SeoKeywords_En ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@CanonicalUrl", (object?)CanonicalUrl ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@CustomFieldsJson", customFieldsJsonString);
                    cmd.Parameters.AddWithValue("@CreatedBy", currentUserId);
                    cmd.Parameters.AddWithValue("@Hits", Hits);

                    await cmd.ExecuteNonQueryAsync();
                    return new JsonResult(new { success = true, isNew = true, message = "تم حفظ المحتوى بنجاح." });
                }
                else
                {
                    // UPDATE
                    string updateQuery = @"
                        UPDATE dbo.Content
                        SET Title = @Title,
                            Title_En = @Title_En,
                            Slug = @Slug,
                            Summary = @Summary,
                            Summary_En = @Summary_En,
                            Content = @Content,
                            Content_En = @Content_En,
                            FeaturedImage = @FeaturedImage,
                            Status = @Status,
                            PublishDate = @PublishDate,
                            CategoryId = @CategoryId,
                            SubCategoryId = @SubCategoryId,
                            SeoTitle = @SeoTitle,
                            SeoTitle_En = @SeoTitle_En,
                            SeoDescription = @SeoDescription,
                            SeoDescription_En = @SeoDescription_En,
                            SeoKeywords = @SeoKeywords,
                            SeoKeywords_En = @SeoKeywords_En,
                            CanonicalUrl = @CanonicalUrl,
                            CustomFieldsJson = @CustomFieldsJson,
                            UpdatedBy = @UpdatedBy,
                            UpdatedAt = GETUTCDATE(),
                            Hits = @Hits
                        WHERE Id = @Id AND IsDeleted = 0";

                    using var cmd = new SqlCommand(updateQuery, connection);
                    cmd.Parameters.AddWithValue("@Id", Id);
                    cmd.Parameters.AddWithValue("@Title", (object?)Title ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Title_En", (object?)Title_En ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Slug", (object?)Slug ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Summary", (object?)Summary ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Summary_En", (object?)Summary_En ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Content", (object?)Content ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Content_En", (object?)Content_En ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@FeaturedImage", (object?)FeaturedImage ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Status", Status);
                    cmd.Parameters.AddWithValue("@PublishDate", (object?)PublishDate ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@CategoryId", (object?)CategoryId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@SubCategoryId", (object?)SubCategoryId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@SeoTitle", (object?)SeoTitle ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@SeoTitle_En", (object?)SeoTitle_En ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@SeoDescription", (object?)SeoDescription ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@SeoDescription_En", (object?)SeoDescription_En ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@SeoKeywords", (object?)SeoKeywords ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@SeoKeywords_En", (object?)SeoKeywords_En ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@CanonicalUrl", (object?)CanonicalUrl ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@CustomFieldsJson", customFieldsJsonString);
                    cmd.Parameters.AddWithValue("@UpdatedBy", currentUserId);
                    cmd.Parameters.AddWithValue("@Hits", Hits);

                    await cmd.ExecuteNonQueryAsync();
                    return new JsonResult(new { success = true, isNew = false, message = "تم تحديث البيانات وحفظ المحتوى بنجاح." });
                }
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = $"فشل حفظ البيانات: {ex.Message}" });
            }
        }

        // =========================================================================
        // AJAX File Upload Handler
        // =========================================================================
        public async Task<IActionResult> OnPostUploadFileAsync(IFormFile file)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return new JsonResult(new { success = false, message = "جلسة العمل انتهت، يرجى تسجيل الدخول." })
                {
                    StatusCode = StatusCodes.Status401Unauthorized
                };
            }

            if (!User.HasClaim("Permission", "Content.Edit") && !User.HasClaim("Permission", "Content.Create"))
            {
                return new JsonResult(new { success = false, message = "ليس لديك صلاحية لرفع الملفات." })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
            }

            if (file == null || file.Length == 0)
            {
                return new JsonResult(new { success = false, message = "لم يتم اختيار ملف." });
            }

            string? connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                return new JsonResult(new { success = false, message = "جملة الاتصال بقاعدة البيانات غير مهيأة." });
            }

            var (maxUploadSizeMb, allowedExtensions) = await UploadValidation.LoadSettingsAsync(connectionString);
            string? validationError = UploadValidation.ValidateFile(file, maxUploadSizeMb, allowedExtensions);
            if (validationError != null)
            {
                return new JsonResult(new { success = false, message = validationError });
            }

            try
            {
                string currentYear = DateTime.Now.Year.ToString();
                string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", currentYear);

                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                string ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                string uniqueFileName = Guid.NewGuid().ToString("N") + ext;
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(fileStream);
                }

                string relativePath = $"/uploads/{currentYear}/{uniqueFileName}";
                return new JsonResult(new { success = true, filePath = relativePath });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = $"فشل رفع الملف: {ex.Message}" });
            }
        }

        // =========================================================================
        // Data Loading Operations
        // =========================================================================
        private async Task LoadMetadataListsAsync(string connectionString)
        {
            AllContentTypes.Clear();
            CategoriesList.Clear();

            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            // 1. Load active content types
            string selectTypes = "SELECT Id, Name, Name_En, Description, Description_En, IconValue FROM dbo.ContentTypes WHERE IsActive = 1 ORDER BY Name ASC";
            using (var cmd = new SqlCommand(selectTypes, connection))
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    AllContentTypes.Add(new ContentTypeFilterDto
                    {
                        Id = reader.GetInt64(0),
                        Name = reader.GetString(1),
                        Name_En = reader.IsDBNull(2) ? null : reader.GetString(2),
                        Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                        Description_En = reader.IsDBNull(4) ? null : reader.GetString(4),
                        IconValue = reader.IsDBNull(5) ? null : reader.GetString(5)
                    });
                }
            }

            // 2. Load active categories
            string selectCategories = "SELECT Id, Name, Name_En, ParentId, ContentTypeId FROM dbo.Categories WHERE IsActive = 1 ORDER BY SortOrder ASC, Name ASC";
            using (var cmd = new SqlCommand(selectCategories, connection))
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    CategoriesList.Add(new CategoryFilterDto
                    {
                        Id = reader.GetInt64(0),
                        Name = reader.GetString(1),
                        Name_En = reader.IsDBNull(2) ? null : reader.GetString(2),
                        ParentId = reader.IsDBNull(3) ? null : reader.GetInt64(3),
                        ContentTypeId = reader.IsDBNull(4) ? null : reader.GetInt64(4)
                    });
                }
            }
        }

        private void ApplyCategoryScopeForContentType()
        {
            bool hasDedicatedCategories = CategoriesList.Any(c => c.ContentTypeId == ContentTypeId);
            long? scopeContentTypeId = hasDedicatedCategories ? ContentTypeId : null;

            CategoriesList = CategoriesList
                .Where(c => c.ContentTypeId == scopeContentTypeId)
                .ToList();

            if (CategoryId.HasValue && !CategoriesList.Any(c => c.Id == CategoryId.Value))
            {
                CategoryId = null;
                SubCategoryId = null;
            }

            if (SubCategoryId.HasValue && !CategoriesList.Any(c => c.Id == SubCategoryId.Value))
            {
                SubCategoryId = null;
            }
        }

        private static async Task EnsureCategoryContentTypeColumnAsync(SqlConnection connection)
        {
            const string query = @"
                IF COL_LENGTH('dbo.Categories', 'ContentTypeId') IS NULL
                BEGIN
                    ALTER TABLE dbo.Categories ADD ContentTypeId BIGINT NULL;
                    CREATE INDEX IX_Categories_ContentTypeId ON dbo.Categories(ContentTypeId, ParentId, SortOrder);
                END";

            using var command = new SqlCommand(query, connection);
            await command.ExecuteNonQueryAsync();
        }

        private static async Task<bool> ValidateCategoryScopeAsync(SqlConnection connection, long contentTypeId, long? categoryId, long? subCategoryId)
        {
            if (!categoryId.HasValue && !subCategoryId.HasValue)
            {
                return true;
            }

            const string dedicatedQuery = "SELECT COUNT(*) FROM dbo.Categories WHERE IsActive = 1 AND ContentTypeId = @ContentTypeId";
            using var dedicatedCommand = new SqlCommand(dedicatedQuery, connection);
            dedicatedCommand.Parameters.AddWithValue("@ContentTypeId", contentTypeId);
            bool hasDedicatedCategories = Convert.ToInt32(await dedicatedCommand.ExecuteScalarAsync()) > 0;
            long? expectedContentTypeId = hasDedicatedCategories ? contentTypeId : null;

            if (categoryId.HasValue && !await CategoryBelongsToScopeAsync(connection, categoryId.Value, expectedContentTypeId, null))
            {
                return false;
            }

            if (subCategoryId.HasValue && !await CategoryBelongsToScopeAsync(connection, subCategoryId.Value, expectedContentTypeId, categoryId))
            {
                return false;
            }

            return true;
        }

        private static async Task<bool> CategoryBelongsToScopeAsync(SqlConnection connection, long categoryId, long? expectedContentTypeId, long? expectedParentId)
        {
            const string query = @"
                SELECT COUNT(*)
                FROM dbo.Categories
                WHERE Id = @Id
                  AND IsActive = 1
                  AND ((@ContentTypeId IS NULL AND ContentTypeId IS NULL) OR ContentTypeId = @ContentTypeId)
                  AND (@ParentIdOptional = 0 OR ParentId = @ParentId)";

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@Id", categoryId);
            command.Parameters.AddWithValue("@ContentTypeId", (object?)expectedContentTypeId ?? DBNull.Value);
            command.Parameters.AddWithValue("@ParentIdOptional", expectedParentId.HasValue ? 1 : 0);
            command.Parameters.AddWithValue("@ParentId", (object?)expectedParentId ?? DBNull.Value);
            return Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
        }

        private async Task<bool> LoadContentRowAsync(string connectionString, long id)
        {
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            string selectContent = @"
                SELECT ContentTypeId, Title, Title_En, Slug, Summary, Summary_En, Content, Content_En, 
                       FeaturedImage, Status, PublishDate, CategoryId, SubCategoryId, SeoTitle, SeoTitle_En, 
                       SeoDescription, SeoDescription_En, SeoKeywords, SeoKeywords_En, CanonicalUrl, 
                       CustomFieldsJson, Hits
                FROM dbo.Content
                WHERE Id = @Id AND IsDeleted = 0";

            using var cmd = new SqlCommand(selectContent, connection);
            cmd.Parameters.AddWithValue("@Id", id);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                ContentTypeId = reader.GetInt64(0);
                Title = reader.IsDBNull(1) ? null : reader.GetString(1);
                Title_En = reader.IsDBNull(2) ? null : reader.GetString(2);
                Slug = reader.IsDBNull(3) ? null : reader.GetString(3);
                Summary = reader.IsDBNull(4) ? null : reader.GetString(4);
                Summary_En = reader.IsDBNull(5) ? null : reader.GetString(5);
                Content = reader.IsDBNull(6) ? null : reader.GetString(6);
                Content_En = reader.IsDBNull(7) ? null : reader.GetString(7);
                FeaturedImage = reader.IsDBNull(8) ? null : reader.GetString(8);
                Status = reader.GetString(9);
                PublishDate = reader.IsDBNull(10) ? null : reader.GetDateTime(10);
                CategoryId = reader.IsDBNull(11) ? null : reader.GetInt64(11);
                SubCategoryId = reader.IsDBNull(12) ? null : reader.GetInt64(12);
                SeoTitle = reader.IsDBNull(13) ? null : reader.GetString(13);
                SeoTitle_En = reader.IsDBNull(14) ? null : reader.GetString(14);
                SeoDescription = reader.IsDBNull(15) ? null : reader.GetString(15);
                SeoDescription_En = reader.IsDBNull(16) ? null : reader.GetString(16);
                SeoKeywords = reader.IsDBNull(17) ? null : reader.GetString(17);
                SeoKeywords_En = reader.IsDBNull(18) ? null : reader.GetString(18);
                CanonicalUrl = reader.IsDBNull(19) ? null : reader.GetString(19);
                CustomFieldsValuesJson = reader.IsDBNull(20) ? "{}" : reader.GetString(20);
                Hits = reader.IsDBNull(21) ? 0 : reader.GetInt32(21);
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
                WHERE ContentTypeId = @ContentTypeId 
                ORDER BY SortOrder ASC";

            using (var command = new SqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@ContentTypeId", typeId);
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var fieldName = reader.GetString(1);
                    var isSystem = ContentTypesModel.PredefinedFields.Any(f => f.FieldName.Equals(fieldName, StringComparison.OrdinalIgnoreCase));
                    var labelEn = reader.IsDBNull(3) ? null : reader.GetString(3);
                    var helpTextEn = reader.IsDBNull(8) ? null : reader.GetString(8);

                    if (isSystem)
                    {
                        var preField = ContentTypesModel.PredefinedFields.FirstOrDefault(f => f.FieldName.Equals(fieldName, StringComparison.OrdinalIgnoreCase));
                        if (preField != null)
                        {
                            if (string.IsNullOrWhiteSpace(labelEn)) labelEn = preField.LabelEn;
                            if (string.IsNullOrWhiteSpace(helpTextEn)) helpTextEn = preField.DescriptionEn;
                        }
                    }

                    dbFields.Add(new CustomFieldDto
                    {
                        Id = reader.GetInt64(0),
                        FieldName = fieldName,
                        Label = reader.GetString(2),
                        Label_En = labelEn,
                        FieldType = reader.GetString(4),
                        Placeholder = reader.IsDBNull(5) ? null : reader.GetString(5),
                        Placeholder_En = reader.IsDBNull(6) ? null : reader.GetString(6),
                        HelpText = reader.IsDBNull(7) ? null : reader.GetString(7),
                        HelpText_En = helpTextEn,
                        IsRequired = reader.GetBoolean(9),
                        IsTranslatable = reader.GetBoolean(10),
                        DefaultValue = reader.IsDBNull(11) ? null : reader.GetString(11),
                        OptionsJson = reader.IsDBNull(12) ? null : reader.GetString(12),
                        SortOrder = reader.GetInt32(13),
                        IsActive = reader.GetBoolean(14),
                        IsSystemField = isSystem
                    });
                }
            }

            // Merge missing predefined fields if needed (e.g. fresh type creation layout)
            var mergedFields = new List<CustomFieldDto>();
            var dbReservedNames = new HashSet<string>(dbFields.Select(f => f.FieldName), StringComparer.OrdinalIgnoreCase);
            mergedFields.AddRange(dbFields);

            int index = 1000;
            foreach (var preField in ContentTypesModel.PredefinedFields)
            {
                if (!dbReservedNames.Contains(preField.FieldName))
                {
                    mergedFields.Add(new CustomFieldDto
                    {
                        Id = 0,
                        FieldName = preField.FieldName,
                        Label = preField.Label,
                        Label_En = preField.LabelEn,
                        FieldType = preField.FieldType,
                        Placeholder = null,
                        Placeholder_En = null,
                        HelpText = preField.Description,
                        HelpText_En = preField.DescriptionEn,
                        IsRequired = false,
                        IsTranslatable = false,
                        DefaultValue = null,
                        OptionsJson = null,
                        SortOrder = index++,
                        IsActive = true,
                        IsSystemField = true
                    });
                }
            }

            var orderedFields = mergedFields.OrderBy(f => f.SortOrder).ToList();
            await ResolveDynamicOptionsAsync(connectionString, orderedFields);
            return orderedFields;
        }

        private static async Task ResolveDynamicOptionsAsync(string connectionString, List<CustomFieldDto> fields)
        {
            foreach (var field in fields.Where(f => (f.FieldType == "Select" || f.FieldType == "MultiSelect") && !string.IsNullOrWhiteSpace(f.OptionsJson)))
            {
                try
                {
                    using var doc = JsonDocument.Parse(field.OptionsJson!);
                    if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                        !doc.RootElement.TryGetProperty("mode", out var modeElement) ||
                        !string.Equals(modeElement.GetString(), "dynamic", StringComparison.OrdinalIgnoreCase))
                    {
                        if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                            doc.RootElement.TryGetProperty("mode", out modeElement) &&
                            string.Equals(modeElement.GetString(), "query", StringComparison.OrdinalIgnoreCase) &&
                            doc.RootElement.TryGetProperty("options", out var queryOptionsElement))
                        {
                            var queryOptions = JsonSerializer.Deserialize<List<SelectOptionDto>>(queryOptionsElement.GetRawText()) ?? new();
                            field.OptionsJson = JsonSerializer.Serialize(queryOptions);
                        }
                        continue;
                    }

                    string source = doc.RootElement.TryGetProperty("source", out var sourceElement) ? sourceElement.GetString() ?? "" : "";
                    long sourceId = doc.RootElement.TryGetProperty("sourceId", out var sourceIdElement) ? sourceIdElement.GetInt64() : 0;
                    if (sourceId <= 0)
                    {
                        continue;
                    }

                    var options = await LoadDynamicOptionsAsync(connectionString, source, sourceId);
                    field.OptionsJson = JsonSerializer.Serialize(options);
                }
                catch
                {
                    // Keep original options JSON if dynamic resolution fails.
                }
            }
        }

        private static async Task<List<SelectOptionDto>> LoadDynamicOptionsAsync(string connectionString, string source, long sourceId)
        {
            var options = new List<SelectOptionDto>();
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
                    options.Add(new SelectOptionDto
                    {
                        value = reader.GetInt64(0).ToString(),
                        label = fullName,
                        labelEn = fullName
                    });
                }
                else
                {
                    options.Add(new SelectOptionDto
                    {
                        value = reader.GetInt64(0).ToString(),
                        label = reader.GetString(1),
                        labelEn = reader.IsDBNull(2) ? reader.GetString(1) : reader.GetString(2)
                    });
                }
            }

            return options;
        }

        private static object JsonElementToObject(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString() ?? string.Empty,
                JsonValueKind.Number => element.TryGetInt64(out var longValue) ? longValue : element.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Array => element.EnumerateArray().Select(JsonElementToObject).ToList(),
                JsonValueKind.Object => element.EnumerateObject().ToDictionary(p => p.Name, p => JsonElementToObject(p.Value)),
                _ => element.GetRawText()
            };
        }

        private string CultureInfoCurrent()
        {
            // Simple helper to match frontend logic
            return "ar"; // C# holds Arabic defaults for system messages, client can show in correct language
        }
    }

    public class SelectOptionDto
    {
        public string value { get; set; } = string.Empty;
        public string label { get; set; } = string.Empty;
        public string? labelEn { get; set; }
    }

}
