using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TalaPress.Pages
{
    public class ContentTypesModel : PageModel
    {
        private readonly IConfiguration _configuration;

        public ContentTypesModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // List of all Content Types
        public List<ContentTypeViewModel> ContentTypeList { get; set; } = new();
        public List<DynamicCategoryOptionDto> MainCategoryOptions { get; set; } = new();
        public List<DynamicCategoryOptionDto> SubCategoryOptions { get; set; } = new();
        public List<DynamicRoleOptionDto> RoleOptions { get; set; } = new();
        public List<DynamicUserOptionDto> RoleUserOptions { get; set; } = new();
        public List<DatabaseTableDto> DatabaseTables { get; set; } = new();
        public List<DatabaseColumnDto> DatabaseColumns { get; set; } = new();

        // Selected Content Type ID for loading into the form
        public long SelectedTypeId { get; set; }

        // Combined list of all 14 system predefined fields (Core & SEO)
        public static readonly List<PredefinedFieldViewModel> PredefinedFields = new()
        {
            new("Title", "العنوان", "Title", "Text", "عنوان المحتوى الرئيسي", "Main content title"),
            new("Slug", "الرابط البديل", "Slug", "Url", "رابط عنوان URL الفرعي", "URL friendly slug identifier"),
            new("Summary", "الملخص", "Summary", "Textarea", "ملخص أو مقدمة موجزة للمحتوى", "Brief summary or introduction of the content"),
            new("Content", "التفاصيل", "Content", "RichText", "المحتوى والفقرات التفصيلية الكاملة", "Full content body and detailed paragraphs"),
            new("FeaturedImage", "الصورة البارزة", "FeaturedImage", "Image", "رابط الصورة البارزة الرئيسية للمحتوى", "Main featured image for the content"),
            new("Status", "الحالة", "Status", "Select", "حالة النشر (مسودة، منشور، مجدول...)", "Publish status (draft, published, scheduled...)"),
            new("PublishDate", "تاريخ النشر", "PublishDate", "DateTime", "تاريخ ووقت جدولة ونشر المحتوى", "Scheduled publish date and time"),
            new("CategoryId", "التصنيف الرئيسي", "CategoryId", "Select", "القسم الرئيسي المرتبط بالمحتوى", "Primary category linked to the content"),
            new("SubCategoryId", "التصنيف الفرعي", "SubCategoryId", "Select", "القسم الفرعي التابع للقسم الرئيسي", "Sub-category under the primary category"),
            new("Hits", "عدد الزيارات (Hits)", "Hits", "Number", "عدد زيارات المنشور من الواجهة الأمامية", "Number of visits/hits from the frontend"),
            new("SeoTitle", "عنوان سيو (SEO Title)", "SEO Title", "Text", "العنوان المخصص للظهور بمحركات البحث", "Custom title optimized for search engines"),
            new("SeoDescription", "وصف سيو (SEO Description)", "SEO Description", "Textarea", "الوصف والنبذة المختصرة لمحركات البحث", "Brief description optimized for search engines"),
            new("SeoKeywords", "الكلمات المفتاحية (SEO Keywords)", "SEO Keywords", "Text", "الكلمات المفتاحية المساعدة للأرشفة", "Keywords to assist in search engine indexing"),
            new("CanonicalUrl", "رابط الكانوينكال (Canonical URL)", "Canonical URL", "Url", "رابط التوجيه الأساسي للروابط المكررة", "Primary URL to prevent duplicate content issues")
        };

        private static readonly HashSet<string> ReservedFieldNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "Id", "Title", "Slug", "Summary", "Content", "FeaturedImage", "Status", "PublishDate", "CategoryId", "SubCategoryId",
            "Hits", "SeoTitle", "SeoDescription", "SeoKeywords", "CanonicalUrl"
        };

        // Form Bindings
        [BindProperty]
        public long Id { get; set; }

        [BindProperty]
        public string Name { get; set; } = string.Empty;

        [BindProperty]
        public string Name_En { get; set; } = string.Empty;

        [BindProperty]
        public string? Description { get; set; }

        [BindProperty]
        public string? Description_En { get; set; }

        [BindProperty]
        public string IconValue { get; set; } = "bi-folder-fill";

        [BindProperty]
        public bool IsActive { get; set; } = true;

        [BindProperty]
        public string CustomFieldsJson { get; set; } = "[]";

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(long? selectedId)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return RedirectToPage("/Login");
            }

            if (!User.HasClaim("Permission", "ContentType.View"))
            {
                TempData["ErrorMessage"] = "ليس لديك صلاحية كافية لعرض أنواع المحتوى.";
                return RedirectToPage("/Index");
            }

            string? connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                ErrorMessage = "جملة الاتصال بقاعدة البيانات غير مهيأة.";
                return Page();
            }

            await LoadContentTypesListAsync(connectionString);
            await LoadDynamicOptionSourcesAsync(connectionString);

            if (selectedId.HasValue && selectedId.Value > 0)
            {
                SelectedTypeId = selectedId.Value;
                var currentType = ContentTypeList.FirstOrDefault(t => t.Id == SelectedTypeId);
                if (currentType != null)
                {
                    Id = currentType.Id;
                    Name = currentType.Name;
                    Name_En = currentType.Name_En ?? string.Empty;
                    Description = currentType.Description;
                    Description_En = currentType.Description_En;
                    IconValue = currentType.IconValue ?? "bi-folder-fill";
                    IsActive = currentType.IsActive;

                    // Load custom fields
                    var fields = await GetCustomFieldsListAsync(connectionString, Id);
                    CustomFieldsJson = JsonSerializer.Serialize(fields);
                }
            }
            else
            {
                // Reset form for fresh creation
                Id = 0;
                Name = string.Empty;
                Name_En = string.Empty;
                Description = string.Empty;
                Description_En = string.Empty;
                IconValue = "bi-folder-fill";
                IsActive = true;

                // Pre-populate with all predefined fields in default order
                var defaultFields = new List<CustomFieldDto>();
                int order = 1;
                foreach (var preField in PredefinedFields)
                {
                    defaultFields.Add(new CustomFieldDto
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
                        SortOrder = order++,
                        IsActive = true,
                        IsSystemField = true
                    });
                }
                CustomFieldsJson = JsonSerializer.Serialize(defaultFields);
            }

            return Page();
        }

        public async Task<IActionResult> OnPostSaveAsync()
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return RedirectToPage("/Login");
            }

            bool canSave = (Id == 0)
                ? User.HasClaim("Permission", "ContentType.Create")
                : User.HasClaim("Permission", "ContentType.Edit");

            if (!canSave)
            {
                TempData["ErrorMessage"] = "ليس لديك صلاحية كافية لحفظ تعديلات أنواع المحتوى.";
                return RedirectToPage("/Index");
            }

            if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Name_En))
            {
                ErrorMessage = "اسم نوع المحتوى بالعربية والإنجليزية مطلوبان.";
                return await OnGetReloadAsync();
            }

            // Parse custom fields JSON
            List<CustomFieldDto> customFields;
            try
            {
                customFields = JsonSerializer.Deserialize<List<CustomFieldDto>>(CustomFieldsJson) ?? new();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"صيغة بيانات الحقول المخصصة غير صحيحة: {ex.Message}";
                return await OnGetReloadAsync();
            }

            // Validate fields
            var processedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var field in customFields)
            {
                if (string.IsNullOrWhiteSpace(field.FieldName) || string.IsNullOrWhiteSpace(field.Label))
                {
                    ErrorMessage = "جميع الحقول يجب أن تحتوي على اسم برمجي وتسمية توضيحية.";
                    return await OnGetReloadAsync();
                }

                if (!Regex.IsMatch(field.FieldName, "^[A-Za-z][A-Za-z0-9_]*$"))
                {
                    ErrorMessage = $"اسم الحقل البرمجي '{field.FieldName}' يجب أن يكون بالإنجليزية، يبدأ بحرف، ويحتوي فقط على أحرف إنجليزية وأرقام وشرطة سفلية.";
                    return await OnGetReloadAsync();
                }

                // Check if it's treated as a custom field (i.e. IsSystemField is false)
                bool isSystemField = ReservedFieldNames.Contains(field.FieldName);
                if (!isSystemField && field.IsSystemField)
                {
                    field.IsSystemField = false;
                }
                else if (isSystemField)
                {
                    field.IsSystemField = true;
                }

                // Custom fields are not allowed to use reserved names
                if (!field.IsSystemField && ReservedFieldNames.Contains(field.FieldName))
                {
                    ErrorMessage = $"اسم الحقل المخصص البرمجي '{field.FieldName}' محجوز من قبل النظام ولا يمكن استخدامه.";
                    return await OnGetReloadAsync();
                }

                // Check duplicate field names
                if (processedNames.Contains(field.FieldName))
                {
                    ErrorMessage = $"اسم الحقل البرمجي '{field.FieldName}' مكرر. يجب أن يكون فريداً لكل نوع محتوى.";
                    return await OnGetReloadAsync();
                }
                processedNames.Add(field.FieldName);
            }

            string? connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                ErrorMessage = "جملة الاتصال بقاعدة البيانات غير مهيأة.";
                return await OnGetReloadAsync();
            }

            try
            {
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();
                using var transaction = connection.BeginTransaction();

                try
                {
                    long typeId = Id;
                    bool isSystem = false;

                    if (typeId > 0)
                    {
                        // Check system state
                        string checkQuery = "SELECT IsSystem FROM dbo.ContentTypes WHERE Id = @Id";
                        using var checkCmd = new SqlCommand(checkQuery, connection, transaction);
                        checkCmd.Parameters.AddWithValue("@Id", typeId);
                        var sysVal = await checkCmd.ExecuteScalarAsync();
                        isSystem = sysVal != null && Convert.ToBoolean(sysVal);
                    }

                    if (typeId == 0)
                    {
                        // Insert new ContentType
                        string insertQuery = @"
                            INSERT INTO dbo.ContentTypes (Name, Name_En, Description, Description_En, IconValue, IsSystem, IsActive, CreatedAt)
                            VALUES (@Name, @Name_En, @Description, @Description_En, @IconValue, 0, @IsActive, GETUTCDATE());
                            SELECT SCOPE_IDENTITY();";

                        using var cmd = new SqlCommand(insertQuery, connection, transaction);
                        cmd.Parameters.AddWithValue("@Name", Name);
                        cmd.Parameters.AddWithValue("@Name_En", Name_En);
                        cmd.Parameters.AddWithValue("@Description", (object?)Description ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Description_En", (object?)Description_En ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@IconValue", IconValue);
                        cmd.Parameters.AddWithValue("@IsActive", IsActive);

                        typeId = Convert.ToInt64(await cmd.ExecuteScalarAsync());
                        SuccessMessage = "تم إضافة نوع المحتوى الجديد بنجاح.";
                    }
                    else
                    {
                        // Update existing ContentType
                        string updateQuery = @"
                            UPDATE dbo.ContentTypes
                            SET Name = @Name, Name_En = @Name_En, Description = @Description, Description_En = @Description_En, 
                                IconValue = @IconValue, IsActive = @IsActive, UpdatedAt = GETUTCDATE()
                            WHERE Id = @Id";

                        using var cmd = new SqlCommand(updateQuery, connection, transaction);
                        cmd.Parameters.AddWithValue("@Id", typeId);
                        cmd.Parameters.AddWithValue("@Name", Name);
                        cmd.Parameters.AddWithValue("@Name_En", Name_En);
                        cmd.Parameters.AddWithValue("@Description", (object?)Description ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Description_En", (object?)Description_En ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@IconValue", IconValue);
                        cmd.Parameters.AddWithValue("@IsActive", IsActive);

                        await cmd.ExecuteNonQueryAsync();
                        SuccessMessage = "تم تحديث نوع المحتوى بنجاح.";
                    }

                    // Synchronize fields: Delete existing and re-insert custom fields
                    string deleteFieldsQuery = "DELETE FROM dbo.ContentTypeFields WHERE ContentTypeId = @ContentTypeId";
                    using var deleteCmd = new SqlCommand(deleteFieldsQuery, connection, transaction);
                    deleteCmd.Parameters.AddWithValue("@ContentTypeId", typeId);
                    await deleteCmd.ExecuteNonQueryAsync();

                    // Insert fields with sort orders
                    string insertFieldQuery = @"
                        INSERT INTO dbo.ContentTypeFields 
                        (ContentTypeId, FieldName, Label, Label_En, FieldType, Placeholder, Placeholder_En, HelpText, HelpText_En, 
                         IsRequired, IsTranslatable, DefaultValue, OptionsJson, SortOrder, IsActive, CreatedAt)
                        VALUES 
                        (@ContentTypeId, @FieldName, @Label, @Label_En, @FieldType, @Placeholder, @Placeholder_En, @HelpText, @HelpText_En, 
                         @IsRequired, @IsTranslatable, @DefaultValue, @OptionsJson, @SortOrder, @IsActive, GETUTCDATE())";

                    for (int i = 0; i < customFields.Count; i++)
                    {
                        var field = customFields[i];
                        using var fCmd = new SqlCommand(insertFieldQuery, connection, transaction);
                        fCmd.Parameters.AddWithValue("@ContentTypeId", typeId);
                        fCmd.Parameters.AddWithValue("@FieldName", field.FieldName);
                        fCmd.Parameters.AddWithValue("@Label", field.Label);
                        fCmd.Parameters.AddWithValue("@Label_En", (object?)field.Label_En ?? DBNull.Value);
                        fCmd.Parameters.AddWithValue("@FieldType", field.FieldType);
                        fCmd.Parameters.AddWithValue("@Placeholder", (object?)field.Placeholder ?? DBNull.Value);
                        fCmd.Parameters.AddWithValue("@Placeholder_En", (object?)field.Placeholder_En ?? DBNull.Value);
                        fCmd.Parameters.AddWithValue("@HelpText", (object?)field.HelpText ?? DBNull.Value);
                        fCmd.Parameters.AddWithValue("@HelpText_En", (object?)field.HelpText_En ?? DBNull.Value);
                        fCmd.Parameters.AddWithValue("@IsRequired", field.IsRequired);
                        fCmd.Parameters.AddWithValue("@IsTranslatable", field.IsTranslatable);
                        fCmd.Parameters.AddWithValue("@DefaultValue", (object?)field.DefaultValue ?? DBNull.Value);
                        fCmd.Parameters.AddWithValue("@OptionsJson", (object?)field.OptionsJson ?? DBNull.Value);
                        fCmd.Parameters.AddWithValue("@SortOrder", i + 1); // Dynamic reordered index
                        fCmd.Parameters.AddWithValue("@IsActive", field.IsActive);

                        await fCmd.ExecuteNonQueryAsync();
                    }

                    await transaction.CommitAsync();
                    Id = typeId; // Set current saved id
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    ErrorMessage = $"حدث خطأ أثناء حفظ البيانات: {ex.Message}";
                    return await OnGetReloadAsync();
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"فشل الاتصال بقاعدة البيانات: {ex.Message}";
                return await OnGetReloadAsync();
            }

            return RedirectToPage(new { selectedId = Id });
        }

        public async Task<IActionResult> OnPostDeleteAsync(long id)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return RedirectToPage("/Login");
            }

            if (!User.HasClaim("Permission", "ContentType.Delete"))
            {
                TempData["ErrorMessage"] = "ليس لديك صلاحية كافية لحذف نوع المحتوى.";
                return RedirectToPage("/Index");
            }

            string? connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                ErrorMessage = "جملة الاتصال بقاعدة البيانات غير مهيأة.";
                return Page();
            }

            try
            {
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                // Double check if system content type
                string checkQuery = "SELECT IsSystem FROM dbo.ContentTypes WHERE Id = @Id";
                using (var cmd = new SqlCommand(checkQuery, connection))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    var sysVal = await cmd.ExecuteScalarAsync();
                    if (sysVal != null && Convert.ToBoolean(sysVal))
                    {
                        ErrorMessage = "لا يمكن حذف أنواع المحتوى الرئيسية والأساسية التابعة للنظام.";
                        return await OnGetReloadAsync();
                    }
                }

                using var transaction = connection.BeginTransaction();
                try
                {
                    // 1. Delete fields
                    string deleteFields = "DELETE FROM dbo.ContentTypeFields WHERE ContentTypeId = @Id";
                    using (var cmd = new SqlCommand(deleteFields, connection, transaction))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    // 2. Delete Content Type
                    string deleteType = "DELETE FROM dbo.ContentTypes WHERE Id = @Id";
                    using (var cmd = new SqlCommand(deleteType, connection, transaction))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    await transaction.CommitAsync();
                    SuccessMessage = "تم حذف نوع المحتوى بالكامل بنجاح.";
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    ErrorMessage = $"خطأ أثناء عملية الحذف: {ex.Message}";
                    return await OnGetReloadAsync();
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"فشل الاتصال بقاعدة البيانات: {ex.Message}";
                return await OnGetReloadAsync();
            }

            return RedirectToPage(new { selectedId = (long?)null });
        }

        public async Task<IActionResult> OnPostCloneAsync(long id)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return RedirectToPage("/Login");
            }

            if (!User.HasClaim("Permission", "ContentType.Create"))
            {
                TempData["ErrorMessage"] = "ليس لديك صلاحية كافية لنسخ أنواع المحتوى.";
                return RedirectToPage("/Index");
            }

            string? connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                ErrorMessage = "جملة الاتصال بقاعدة البيانات غير مهيأة.";
                return Page();
            }

            try
            {
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                // Fetch original details
                string originQuery = "SELECT Name, Name_En, Description, Description_En, IconValue FROM dbo.ContentTypes WHERE Id = @Id";
                string origName = "";
                string origNameEn = "";
                string? origDesc = "";
                string? origDescEn = "";
                string origIcon = "bi-folder-fill";

                using (var cmd = new SqlCommand(originQuery, connection))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    using var reader = await cmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        origName = reader.GetString(0);
                        origNameEn = reader.IsDBNull(1) ? "" : reader.GetString(1);
                        origDesc = reader.IsDBNull(2) ? "" : reader.GetString(2);
                        origDescEn = reader.IsDBNull(3) ? "" : reader.GetString(3);
                        origIcon = reader.IsDBNull(4) ? "bi-folder-fill" : reader.GetString(4);
                    }
                    else
                    {
                        ErrorMessage = "نوع المحتوى الأصلي غير موجود.";
                        return await OnGetReloadAsync();
                    }
                }

                // Load original custom fields
                var originalFields = await GetCustomFieldsListAsync(connectionString, id);

                using var transaction = connection.BeginTransaction();
                try
                {
                    // Create Cloned Type (Non-system)
                    string cloneName = $"{origName} Copy";
                    string cloneNameEn = string.IsNullOrEmpty(origNameEn) ? "" : $"{origNameEn} Copy";
                    string cloneQuery = @"
                        INSERT INTO dbo.ContentTypes (Name, Name_En, Description, Description_En, IconValue, IsSystem, IsActive, CreatedAt)
                        VALUES (@Name, @Name_En, @Description, @Description_En, @IconValue, 0, 1, GETUTCDATE());
                        SELECT SCOPE_IDENTITY();";

                    long newTypeId;
                    using (var cmd = new SqlCommand(cloneQuery, connection, transaction))
                    {
                        cmd.Parameters.AddWithValue("@Name", cloneName);
                        cmd.Parameters.AddWithValue("@Name_En", cloneNameEn);
                        cmd.Parameters.AddWithValue("@Description", string.IsNullOrEmpty(origDesc) ? DBNull.Value : origDesc);
                        cmd.Parameters.AddWithValue("@Description_En", string.IsNullOrEmpty(origDescEn) ? DBNull.Value : origDescEn);
                        cmd.Parameters.AddWithValue("@IconValue", origIcon);

                        newTypeId = Convert.ToInt64(await cmd.ExecuteScalarAsync());
                    }

                    // Clone Custom Fields
                    string insertFieldQuery = @"
                        INSERT INTO dbo.ContentTypeFields 
                        (ContentTypeId, FieldName, Label, Label_En, FieldType, Placeholder, Placeholder_En, HelpText, HelpText_En, 
                         IsRequired, IsTranslatable, DefaultValue, OptionsJson, SortOrder, IsActive, CreatedAt)
                        VALUES 
                        (@ContentTypeId, @FieldName, @Label, @Label_En, @FieldType, @Placeholder, @Placeholder_En, @HelpText, @HelpText_En, 
                         @IsRequired, @IsTranslatable, @DefaultValue, @OptionsJson, @SortOrder, @IsActive, GETUTCDATE())";

                    foreach (var field in originalFields)
                    {
                        using var cmd = new SqlCommand(insertFieldQuery, connection, transaction);
                        cmd.Parameters.AddWithValue("@ContentTypeId", newTypeId);
                        cmd.Parameters.AddWithValue("@FieldName", field.FieldName);
                        cmd.Parameters.AddWithValue("@Label", field.Label);
                        cmd.Parameters.AddWithValue("@Label_En", (object?)field.Label_En ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@FieldType", field.FieldType);
                        cmd.Parameters.AddWithValue("@Placeholder", (object?)field.Placeholder ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Placeholder_En", (object?)field.Placeholder_En ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@HelpText", (object?)field.HelpText ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@HelpText_En", (object?)field.HelpText_En ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@IsRequired", field.IsRequired);
                        cmd.Parameters.AddWithValue("@IsTranslatable", field.IsTranslatable);
                        cmd.Parameters.AddWithValue("@DefaultValue", (object?)field.DefaultValue ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@OptionsJson", (object?)field.OptionsJson ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@SortOrder", field.SortOrder);
                        cmd.Parameters.AddWithValue("@IsActive", field.IsActive);

                        await cmd.ExecuteNonQueryAsync();
                    }

                    await transaction.CommitAsync();
                    SuccessMessage = $"تم نسخ نوع المحتوى كـ '{cloneName}' بنجاح.";
                    Id = newTypeId;
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    ErrorMessage = $"خطأ أثناء عملية النسخ المتطابق: {ex.Message}";
                    return await OnGetReloadAsync();
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"فشل الاتصال بقاعدة البيانات: {ex.Message}";
                return await OnGetReloadAsync();
            }

            return RedirectToPage(new { selectedId = Id });
        }

        public async Task<IActionResult> OnPostExecuteQueryAsync([FromBody] QueryBuilderRequest request)
        {
            if (User.Identity?.IsAuthenticated != true || !User.HasClaim("Permission", "ContentType.View"))
            {
                return new JsonResult(new { success = false, message = "غير مصرح بالدخول." });
            }

            string? connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return new JsonResult(new { success = false, message = "جملة الاتصال بقاعدة البيانات غير مهيأة." });
            }

            try
            {
                if (string.IsNullOrWhiteSpace(request.TableName))
                {
                    return new JsonResult(new { success = false, message = "يرجى اختيار جدول." });
                }

                var schema = await LoadTableSchemaAsync(connectionString, request.TableName);
                if (schema.Count == 0)
                {
                    return new JsonResult(new { success = false, message = "الجدول المحدد غير موجود أو لا يحتوي أعمدة." });
                }

                var parameters = new List<SqlParameter>();
                string whereSql = BuildQueryWhere(request, parameters, schema);
                string tableSql = QuoteIdentifier(request.TableName);
                string orderColumn = schema.ContainsKey("Id") ? "Id" : schema.Keys.First();
                string query = $@"
                    SELECT TOP (100) *
                    FROM dbo.{tableSql} q
                    WHERE {whereSql}
                    ORDER BY q.{QuoteIdentifier(orderColumn)} DESC";

                var results = new List<object>();
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();
                using var command = new SqlCommand(query, connection);
                foreach (var parameter in parameters)
                {
                    command.Parameters.Add(parameter);
                }

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var value = ReadColumnValue(reader, schema.ContainsKey("Id") ? "Id" : orderColumn);
                    var label = ReadBestLabel(reader, preferEnglish: false) ?? value;
                    var labelEn = ReadBestLabel(reader, preferEnglish: true) ?? label;

                    results.Add(new
                    {
                        value,
                        label,
                        labelEn
                    });
                }

                return new JsonResult(new { success = true, results });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }

        // =============================================
        // Shared: Sensitive column names that must NEVER
        // appear in any query result or API response.
        // =============================================
        public static readonly HashSet<string> SensitiveColumns = new(StringComparer.OrdinalIgnoreCase)
        {
            "PasswordHash", "Password", "PasswordSalt",
            "Token", "AccessToken", "RefreshToken", "ResetToken", "ConfirmToken",
            "Secret", "ApiSecret", "PrivateKey", "ClientSecret",
            "Pin", "Otp", "TwoFactorSecret",
            "SecurityStamp", "ConcurrencyStamp"
        };

        public async Task<IActionResult> OnPostExecuteSqlQueryAsync([FromBody] RawSqlQueryRequest request)
        {
            if (User.Identity?.IsAuthenticated != true || !User.HasClaim("Permission", "ContentType.View"))
            {
                return new JsonResult(new { success = false, message = "غير مصرح بالدخول." });
            }

            string? connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return new JsonResult(new { success = false, message = "جملة الاتصال بقاعدة البيانات غير مهيأة." });
            }

            try
            {
                var sql = (request.Sql ?? "").Trim();
                if (string.IsNullOrWhiteSpace(sql))
                {
                    return new JsonResult(new { success = false, message = "الاستعلام فارغ." });
                }

                // Security: only allow SELECT statements
                var upperSql = sql.ToUpperInvariant();
                if (!upperSql.TrimStart().StartsWith("SELECT"))
                {
                    return new JsonResult(new { success = false, message = "يُسمح فقط بجمل SELECT. Only SELECT statements are allowed." });
                }

                // Block dangerous keywords
                var blockedKeywords = new[] { "INSERT", "UPDATE", "DELETE", "DROP", "TRUNCATE", "ALTER", "CREATE", "EXEC", "EXECUTE", "GRANT", "REVOKE", "MERGE", "BULK" };
                foreach (var kw in blockedKeywords)
                {
                    // Check as whole word using regex
                    if (Regex.IsMatch(upperSql, $@"\b{kw}\b"))
                    {
                        return new JsonResult(new { success = false, message = $"الاستعلام يحتوي على عملية محظورة: {kw}." });
                    }
                }

                // Wrap with TOP safety limit
                string safeSql = sql;
                if (!upperSql.Contains("TOP ") && !upperSql.Contains("TOP("))
                {
                    // Insert TOP 200 after SELECT keyword
                    int selectIdx = upperSql.IndexOf("SELECT");
                    safeSql = sql.Substring(0, selectIdx + 6) + " TOP (200) " + sql.Substring(selectIdx + 6);
                }

                var results = new List<object>();
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();
                using var command = new SqlCommand(safeSql, connection);
                command.CommandTimeout = 15;
                using var reader = await command.ExecuteReaderAsync();

                // Detect column names
                var columnNames = new List<string>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    columnNames.Add(reader.GetName(i));
                }

                if (columnNames.Count < 1)
                {
                    return new JsonResult(new { success = false, message = "الاستعلام لم يُرجع أي أعمدة." });
                }

                // Security: reject queries that expose sensitive columns
                var blockedCols = columnNames.Where(c => SensitiveColumns.Contains(c)).ToList();
                if (blockedCols.Count > 0)
                {
                    return new JsonResult(new
                    {
                        success = false,
                        message = $"الاستعلام يحتوي على أعمدة محظورة لأسباب أمنية: {string.Join(", ", blockedCols)}. يُرجى إزالتها من الاستعلام."
                    });
                }

                // Intelligently detect value, label, labelEn columns
                string valueCol = columnNames[0]; // First column = value
                string labelArCol = columnNames.Count > 1 ? columnNames[1] : columnNames[0]; // Second = label
                string? labelEnCol = null;

                // Try to find English column
                string[] enCandidates = { "Name_En", "Title_En", "Label_En", "FullName_En", "Value_En" };
                string[] arCandidates = { "Name", "Title", "Label", "FullName", "Username", "Email" };

                foreach (var c in arCandidates)
                {
                    if (columnNames.Any(col => col.Equals(c, StringComparison.OrdinalIgnoreCase)))
                    {
                        labelArCol = columnNames.First(col => col.Equals(c, StringComparison.OrdinalIgnoreCase));
                        break;
                    }
                }
                foreach (var c in enCandidates)
                {
                    if (columnNames.Any(col => col.Equals(c, StringComparison.OrdinalIgnoreCase)))
                    {
                        labelEnCol = columnNames.First(col => col.Equals(c, StringComparison.OrdinalIgnoreCase));
                        break;
                    }
                }

                while (await reader.ReadAsync())
                {
                    string? value = null;
                    string? labelAr = null;
                    string? labelEn = null;

                    try { value = reader.IsDBNull(reader.GetOrdinal(valueCol)) ? null : Convert.ToString(reader[valueCol]); } catch { }
                    try { labelAr = reader.IsDBNull(reader.GetOrdinal(labelArCol)) ? null : Convert.ToString(reader[labelArCol]); } catch { }
                    if (labelEnCol != null)
                    {
                        try { labelEn = reader.IsDBNull(reader.GetOrdinal(labelEnCol)) ? null : Convert.ToString(reader[labelEnCol]); } catch { }
                    }

                    if (string.IsNullOrEmpty(value)) continue;

                    results.Add(new
                    {
                        value = value,
                        label = labelAr ?? value,
                        labelEn = labelEn ?? labelAr ?? value
                    });
                }

                return new JsonResult(new { success = true, results, columns = columnNames });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }


        // Helpers
        private async Task LoadContentTypesListAsync(string connectionString)
        {
            ContentTypeList.Clear();
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            string query = "SELECT Id, Name, Name_En, Description, Description_En, IconValue, IsSystem, IsActive FROM dbo.ContentTypes ORDER BY IsSystem DESC, Name ASC";
            using var command = new SqlCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                ContentTypeList.Add(new ContentTypeViewModel
                {
                    Id = reader.GetInt64(0),
                    Name = reader.GetString(1),
                    Name_En = reader.IsDBNull(2) ? null : reader.GetString(2),
                    Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Description_En = reader.IsDBNull(4) ? null : reader.GetString(4),
                    IconValue = reader.IsDBNull(5) ? null : reader.GetString(5),
                    IsSystem = reader.GetBoolean(6),
                    IsActive = reader.GetBoolean(7)
                });
            }
        }

        private static string BuildQueryWhere(QueryBuilderRequest request, List<SqlParameter> parameters, Dictionary<string, string> schema)
        {
            var parts = new List<string>();
            foreach (var rule in request.Rules ?? new())
            {
                string? part = BuildRuleSql(rule, parameters, schema);
                if (!string.IsNullOrWhiteSpace(part))
                {
                    parts.Add(part);
                }
            }

            foreach (var group in request.Groups ?? new())
            {
                string groupSql = BuildQueryWhere(group, parameters, schema);
                if (!string.IsNullOrWhiteSpace(groupSql))
                {
                    parts.Add($"({groupSql})");
                }
            }

            if (parts.Count == 0)
            {
                return "1 = 1";
            }

            string logic = string.Equals(request.Logic, "OR", StringComparison.OrdinalIgnoreCase) ? " OR " : " AND ";
            return string.Join(logic, parts);
        }

        private static string? BuildRuleSql(QueryRuleDto rule, List<SqlParameter> parameters, Dictionary<string, string> schema)
        {
            string fieldName = rule.Field ?? string.Empty;
            if (!schema.ContainsKey(fieldName))
            {
                return null;
            }

            string op = rule.Operator ?? "Equals";
            bool noValue = op is "IsBlank" or "IsNotBlank";
            string parameterName = $"@q{parameters.Count}";
            string rawValue = rule.Value ?? string.Empty;
            string column = $"q.{QuoteIdentifier(fieldName)}";

            if (!noValue)
            {
                object parameterValue = op switch
                {
                    "Contains" => $"%{rawValue}%",
                    "StartsWith" => $"{rawValue}%",
                    "EndsWith" => $"%{rawValue}",
                    _ => rawValue
                };
                parameters.Add(new SqlParameter(parameterName, parameterValue));
            }

            return op switch
            {
                "DoesNotEqual" => $"ISNULL(CONVERT(NVARCHAR(MAX), {column}), '') <> {parameterName}",
                "Contains" or "StartsWith" or "EndsWith" => $"CONVERT(NVARCHAR(MAX), {column}) LIKE {parameterName}",
                "GreaterThan" => $"{column} > {parameterName}",
                "LessThan" => $"{column} < {parameterName}",
                "IsBlank" => $"({column} IS NULL OR CONVERT(NVARCHAR(MAX), {column}) = '')",
                "IsNotBlank" => $"({column} IS NOT NULL AND CONVERT(NVARCHAR(MAX), {column}) <> '')",
                "In" => $"CONVERT(NVARCHAR(MAX), {column}) IN (SELECT TRIM(value) FROM STRING_SPLIT({parameterName}, ','))",
                _ => $"ISNULL(CONVERT(NVARCHAR(MAX), {column}), '') = {parameterName}"
            };
        }

        private static async Task<Dictionary<string, string>> LoadTableSchemaAsync(string connectionString, string tableName)
        {
            var schema = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            const string query = @"
                SELECT COLUMN_NAME, DATA_TYPE
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = @TableName
                ORDER BY ORDINAL_POSITION";

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@TableName", tableName);
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                schema[reader.GetString(0)] = reader.GetString(1);
            }

            return schema;
        }

        private static string QuoteIdentifier(string value) => $"[{value.Replace("]", "]] ").Replace("]] ", "]]")}]";

        private static string? ReadColumnValue(SqlDataReader reader, string columnName)
        {
            try
            {
                int ordinal = reader.GetOrdinal(columnName);
                return reader.IsDBNull(ordinal) ? null : Convert.ToString(reader.GetValue(ordinal));
            }
            catch
            {
                return null;
            }
        }

        private static string? ReadBestLabel(SqlDataReader reader, bool preferEnglish)
        {
            string[] candidates = preferEnglish
                ? new[] { "Title_En", "Name_En", "Label_En", "FullName", "Username", "Title", "Name", "Label", "Email" }
                : new[] { "Title", "Name", "Label", "FullName", "Username", "Title_En", "Name_En", "Label_En", "Email" };

            foreach (var candidate in candidates)
            {
                var value = ReadColumnValue(reader, candidate);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return null;
        }

        private async Task LoadDynamicOptionSourcesAsync(string connectionString)
        {
            MainCategoryOptions.Clear();
            SubCategoryOptions.Clear();
            RoleOptions.Clear();
            RoleUserOptions.Clear();
            DatabaseTables.Clear();
            DatabaseColumns.Clear();

            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            string schemaQuery = @"
                SELECT t.TABLE_NAME, c.COLUMN_NAME, c.DATA_TYPE
                FROM INFORMATION_SCHEMA.TABLES t
                INNER JOIN INFORMATION_SCHEMA.COLUMNS c ON t.TABLE_SCHEMA = c.TABLE_SCHEMA AND t.TABLE_NAME = c.TABLE_NAME
                WHERE t.TABLE_SCHEMA = 'dbo'
                  AND t.TABLE_TYPE = 'BASE TABLE'
                ORDER BY t.TABLE_NAME, c.ORDINAL_POSITION";
            using (var command = new SqlCommand(schemaQuery, connection))
            using (var reader = await command.ExecuteReaderAsync())
            {
                var tableNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                while (await reader.ReadAsync())
                {
                    string tableName = reader.GetString(0);
                    if (tableNames.Add(tableName))
                    {
                        DatabaseTables.Add(new DatabaseTableDto { Name = tableName });
                    }

                    DatabaseColumns.Add(new DatabaseColumnDto
                    {
                        TableName = tableName,
                        Name = reader.GetString(1),
                        DataType = reader.GetString(2)
                    });
                }
            }

            string categoryQuery = @"
                SELECT Id, Name, Name_En, ParentId
                FROM dbo.Categories
                WHERE IsActive = 1
                ORDER BY SortOrder ASC, Name ASC";
            using (var command = new SqlCommand(categoryQuery, connection))
            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var item = new DynamicCategoryOptionDto
                    {
                        Id = reader.GetInt64(0),
                        Name = reader.GetString(1),
                        Name_En = reader.IsDBNull(2) ? null : reader.GetString(2),
                        ParentId = reader.IsDBNull(3) ? null : reader.GetInt64(3)
                    };

                    if (item.ParentId.HasValue)
                    {
                        SubCategoryOptions.Add(item);
                    }
                    else
                    {
                        MainCategoryOptions.Add(item);
                    }
                }
            }

            string rolesQuery = "SELECT Id, Name, Name_En FROM dbo.Roles ORDER BY Name ASC";
            using (var command = new SqlCommand(rolesQuery, connection))
            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    RoleOptions.Add(new DynamicRoleOptionDto
                    {
                        Id = reader.GetInt64(0),
                        Name = reader.GetString(1),
                        Name_En = reader.IsDBNull(2) ? null : reader.GetString(2)
                    });
                }
            }

            string usersQuery = @"
                SELECT ur.RoleId, u.Id, u.FullName, u.Username, u.Email
                FROM dbo.UserRoles ur
                INNER JOIN dbo.Users u ON ur.UserId = u.Id
                WHERE u.IsActive = 1
                ORDER BY u.FullName, u.Username";
            using (var command = new SqlCommand(usersQuery, connection))
            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    RoleUserOptions.Add(new DynamicUserOptionDto
                    {
                        RoleId = reader.GetInt64(0),
                        Id = reader.GetInt64(1),
                        FullName = reader.IsDBNull(2) ? reader.GetString(3) : reader.GetString(2),
                        Username = reader.GetString(3),
                        Email = reader.IsDBNull(4) ? null : reader.GetString(4)
                    });
                }
            }
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

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@ContentTypeId", typeId);
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var fieldName = reader.GetString(1);
                var isSystem = ReservedFieldNames.Contains(fieldName);
                var labelEn = reader.IsDBNull(3) ? null : reader.GetString(3);
                var helpTextEn = reader.IsDBNull(8) ? null : reader.GetString(8);

                if (isSystem)
                {
                    var preField = PredefinedFields.FirstOrDefault(f => f.FieldName.Equals(fieldName, StringComparison.OrdinalIgnoreCase));
                    if (preField != null)
                    {
                        if (string.IsNullOrWhiteSpace(labelEn))
                        {
                            labelEn = preField.LabelEn;
                        }
                        if (string.IsNullOrWhiteSpace(helpTextEn))
                        {
                            helpTextEn = preField.DescriptionEn;
                        }
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

            // Merge predefined fields if they are missing from dbFields
            var mergedFields = new List<CustomFieldDto>();
            var dbReservedNames = new HashSet<string>(dbFields.Select(f => f.FieldName), StringComparer.OrdinalIgnoreCase);

            mergedFields.AddRange(dbFields);

            // Now add any predefined fields that aren't in the database yet
            int index = 1000; // Large number to put them at the end if they are newly added
            foreach (var preField in PredefinedFields)
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
                        IsActive = true, // Visible by default
                        IsSystemField = true
                    });
                }
            }

            // Sort by SortOrder
            return mergedFields.OrderBy(f => f.SortOrder).ToList();
        }

        private async Task<IActionResult> OnGetReloadAsync()
        {
            string? connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (!string.IsNullOrEmpty(connectionString))
            {
                await LoadContentTypesListAsync(connectionString);
                await LoadDynamicOptionSourcesAsync(connectionString);
            }
            return Page();
        }
    }

    // ViewModels and DTOs
    public class ContentTypeViewModel
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Name_En { get; set; }
        public string? Description { get; set; }
        public string? Description_En { get; set; }
        public string? IconValue { get; set; }
        public bool IsSystem { get; set; }
        public bool IsActive { get; set; }
    }

    public class PredefinedFieldViewModel
    {
        public string FieldName { get; }
        public string Label { get; }
        public string LabelEn { get; }
        public string FieldType { get; }
        public string Description { get; }
        public string DescriptionEn { get; }

        public PredefinedFieldViewModel(string name, string label, string labelEn, string type, string desc, string descEn)
        {
            FieldName = name;
            Label = label;
            LabelEn = labelEn;
            FieldType = type;
            Description = desc;
            DescriptionEn = descEn;
        }
    }

    public class CustomFieldDto
    {
        public long Id { get; set; }
        public string FieldName { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string? Label_En { get; set; }
        public string FieldType { get; set; } = "Text";
        public string? Placeholder { get; set; }
        public string? Placeholder_En { get; set; }
        public string? HelpText { get; set; }
        public string? HelpText_En { get; set; }
        public bool IsRequired { get; set; }
        public bool IsTranslatable { get; set; }
        public string? DefaultValue { get; set; }
        public string? OptionsJson { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsSystemField { get; set; }
    }

    public class DynamicCategoryOptionDto
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Name_En { get; set; }
        public long? ParentId { get; set; }
    }

    public class DynamicRoleOptionDto
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Name_En { get; set; }
    }

    public class DynamicUserOptionDto
    {
        public long RoleId { get; set; }
        public long Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string? Email { get; set; }
    }

    public class DatabaseTableDto
    {
        public string Name { get; set; } = string.Empty;
    }

    public class DatabaseColumnDto
    {
        public string TableName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string DataType { get; set; } = string.Empty;
    }

    public class QueryBuilderRequest
    {
        public string TableName { get; set; } = string.Empty;
        public string Logic { get; set; } = "AND";
        public List<QueryRuleDto> Rules { get; set; } = new();
        public List<QueryBuilderRequest> Groups { get; set; } = new();
    }

    public class QueryRuleDto
    {
        public string Field { get; set; } = "Title";
        public string Operator { get; set; } = "Contains";
        public string? Value { get; set; }
    }

    public class RawSqlQueryRequest
    {
        public string Sql { get; set; } = string.Empty;
    }
}
