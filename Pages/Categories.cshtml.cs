using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace TalaPress.Pages
{
    public class CategoriesModel : PageModel
    {
        private readonly IConfiguration _configuration;

        public CategoriesModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // List of categories to render in the hierarchical tree
        public List<CategoryViewModel> CategoryList { get; set; } = new();
        public List<ContentTypeFilterDto> ContentTypesList { get; set; } = new();

        [BindProperty]
        public long Id { get; set; }

        [BindProperty]
        public string Name { get; set; } = string.Empty;

        [BindProperty]
        public string? Name_En { get; set; }

        [BindProperty]
        public string? Slug { get; set; }

        [BindProperty]
        public long? ParentId { get; set; }

        [BindProperty]
        public long? ContentTypeId { get; set; }

        [BindProperty]
        public string? IconValue { get; set; }

        [BindProperty]
        public int SortOrder { get; set; }

        [BindProperty]
        public bool IsActive { get; set; } = true;

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return RedirectToPage("/Login");
            }

            if (!User.HasClaim("Permission", "Category.View"))
            {
                TempData["ErrorMessage"] = "ليس لديك صلاحية كافية لعرض صفحة إدارة التصنيفات.";
                return RedirectToPage("/Index");
            }

            await LoadMetadataAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostSaveAsync()
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return RedirectToPage("/Login");
            }

            bool canSave = (Id == 0)
                ? User.HasClaim("Permission", "Category.Create")
                : User.HasClaim("Permission", "Category.Edit");

            if (!canSave)
            {
                TempData["ErrorMessage"] = "ليس لديك صلاحية كافية لحفظ التصنيفات.";
                return RedirectToPage("/Index");
            }

            if (string.IsNullOrWhiteSpace(Name))
            {
                ErrorMessage = "اسم الفئة باللغة العربية مطلوب.";
                await LoadMetadataAsync();
                return Page();
            }

            // Auto-generate slug if empty
            if (string.IsNullOrWhiteSpace(Slug))
            {
                string baseText = !string.IsNullOrWhiteSpace(Name_En) ? Name_En : Name;
                Slug = GenerateSlug(baseText);
            }
            else
            {
                Slug = GenerateSlug(Slug);
            }

            string? connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                ErrorMessage = "جملة الاتصال بقاعدة البيانات غير مهيأة.";
                await LoadMetadataAsync();
                return Page();
            }

            try
            {
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();
                await EnsureCategoryContentTypeColumnAsync(connection);

                if (ContentTypeId.HasValue && ContentTypeId.Value <= 0)
                {
                    ContentTypeId = null;
                }

                if (ParentId.HasValue)
                {
                    string parentScopeQuery = "SELECT ContentTypeId FROM dbo.Categories WHERE Id = @ParentId";
                    using var parentScopeCmd = new SqlCommand(parentScopeQuery, connection);
                    parentScopeCmd.Parameters.AddWithValue("@ParentId", ParentId.Value);
                    object? parentScopeValue = await parentScopeCmd.ExecuteScalarAsync();
                    long? parentContentTypeId = parentScopeValue == null || parentScopeValue == DBNull.Value ? null : Convert.ToInt64(parentScopeValue);
                    if (parentContentTypeId != ContentTypeId)
                    {
                        ErrorMessage = "لا يمكن اختيار فئة أب مرتبطة بنوع محتوى مختلف.";
                        await LoadMetadataAsync();
                        return Page();
                    }
                }

                if (Id == 0)
                {
                    // INSERT new category
                    string insertQuery = @"
                        INSERT INTO dbo.Categories (Name, Name_En, Slug, ParentId, ContentTypeId, IconValue, Image, SortOrder, IsActive, CreatedAt)
                        VALUES (@Name, @Name_En, @Slug, @ParentId, @ContentTypeId, @IconValue, NULL, @SortOrder, @IsActive, GETUTCDATE())";

                    using var command = new SqlCommand(insertQuery, connection);
                    command.Parameters.AddWithValue("@Name", Name);
                    command.Parameters.AddWithValue("@Name_En", (object?)Name_En ?? DBNull.Value);
                    command.Parameters.AddWithValue("@Slug", Slug);
                    command.Parameters.AddWithValue("@ParentId", (object?)ParentId ?? DBNull.Value);
                    command.Parameters.AddWithValue("@ContentTypeId", (object?)ContentTypeId ?? DBNull.Value);
                    command.Parameters.AddWithValue("@IconValue", (object?)IconValue ?? DBNull.Value);
                    command.Parameters.AddWithValue("@SortOrder", SortOrder);
                    command.Parameters.AddWithValue("@IsActive", IsActive);

                    await command.ExecuteNonQueryAsync();
                    SuccessMessage = "تم إضافة الفئة بنجاح.";
                }
                else
                {
                    // UPDATE existing category
                    // Prevent circular dependency (setting parent to self or child)
                    if (ParentId == Id)
                    {
                        ErrorMessage = "لا يمكن تعيين الفئة كأب لنفسها.";
                await LoadMetadataAsync();
                        return Page();
                    }

                    string updateQuery = @"
                        UPDATE dbo.Categories 
                        SET Name = @Name, Name_En = @Name_En, Slug = @Slug, ParentId = @ParentId, ContentTypeId = @ContentTypeId,
                            IconValue = @IconValue, SortOrder = @SortOrder, IsActive = @IsActive, UpdatedAt = GETUTCDATE()
                        WHERE Id = @Id";

                    using var command = new SqlCommand(updateQuery, connection);
                    command.Parameters.AddWithValue("@Id", Id);
                    command.Parameters.AddWithValue("@Name", Name);
                    command.Parameters.AddWithValue("@Name_En", (object?)Name_En ?? DBNull.Value);
                    command.Parameters.AddWithValue("@Slug", Slug);
                    command.Parameters.AddWithValue("@ParentId", (object?)ParentId ?? DBNull.Value);
                    command.Parameters.AddWithValue("@ContentTypeId", (object?)ContentTypeId ?? DBNull.Value);
                    command.Parameters.AddWithValue("@IconValue", (object?)IconValue ?? DBNull.Value);
                    command.Parameters.AddWithValue("@SortOrder", SortOrder);
                    command.Parameters.AddWithValue("@IsActive", IsActive);

                    await command.ExecuteNonQueryAsync();
                    SuccessMessage = "تم تحديث الفئة بنجاح.";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"حدث خطأ أثناء حفظ الفئة: {ex.Message}";
                await LoadMetadataAsync();
                return Page();
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAsync(long id)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return RedirectToPage("/Login");
            }

            if (!User.HasClaim("Permission", "Category.Delete"))
            {
                TempData["ErrorMessage"] = "ليس لديك صلاحية كافية لحذف التصنيفات.";
                return RedirectToPage("/Index");
            }

            string? connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                ErrorMessage = "جملة الاتصال بقاعدة البيانات غير مهيأة.";
                return RedirectToPage();
            }

            try
            {
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();
                await EnsureCategoryContentTypeColumnAsync(connection);

                if (ContentTypeId.HasValue && ContentTypeId.Value <= 0)
                {
                    ContentTypeId = null;
                }

                // Check for child categories
                string checkQuery = "SELECT COUNT(*) FROM dbo.Categories WHERE ParentId = @Id";
                using var checkCmd = new SqlCommand(checkQuery, connection);
                checkCmd.Parameters.AddWithValue("@Id", id);
                int childCount = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());

                if (childCount > 0)
                {
                    ErrorMessage = "لا يمكن حذف هذه الفئة لأنها تحتوي على فئات فرعية. يرجى حذف الفئات الفرعية أولاً.";
                    return RedirectToPage();
                }

                string activeContentCheckQuery = @"
                    SELECT COUNT(*)
                    FROM dbo.Content
                    WHERE IsDeleted = 0
                      AND (CategoryId = @Id OR SubCategoryId = @Id)";
                using var activeContentCheckCmd = new SqlCommand(activeContentCheckQuery, connection);
                activeContentCheckCmd.Parameters.AddWithValue("@Id", id);
                int activeContentCount = Convert.ToInt32(await activeContentCheckCmd.ExecuteScalarAsync());

                if (activeContentCount > 0)
                {
                    ErrorMessage = $"لا يمكن حذف هذه الفئة لأنها مرتبطة بـ {activeContentCount} عنصر محتوى نشط. يرجى تعديل المحتوى المرتبط أولاً.";
                    return RedirectToPage();
                }

                string detachDeletedContentQuery = @"
                    UPDATE dbo.Content
                    SET CategoryId = CASE WHEN CategoryId = @Id THEN NULL ELSE CategoryId END,
                        SubCategoryId = CASE WHEN SubCategoryId = @Id THEN NULL ELSE SubCategoryId END,
                        UpdatedAt = GETUTCDATE()
                    WHERE IsDeleted = 1
                      AND (CategoryId = @Id OR SubCategoryId = @Id)";
                using var detachDeletedContentCmd = new SqlCommand(detachDeletedContentQuery, connection);
                detachDeletedContentCmd.Parameters.AddWithValue("@Id", id);
                await detachDeletedContentCmd.ExecuteNonQueryAsync();

                // Delete the category
                string deleteQuery = "DELETE FROM dbo.Categories WHERE Id = @Id";
                using var deleteCmd = new SqlCommand(deleteQuery, connection);
                deleteCmd.Parameters.AddWithValue("@Id", id);
                await deleteCmd.ExecuteNonQueryAsync();

                SuccessMessage = "تم حذف الفئة بنجاح.";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"حدث خطأ أثناء حذف الفئة: {ex.Message}";
            }

            return RedirectToPage();
        }

        private async Task LoadMetadataAsync()
        {
            string? connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString)) return;

            CategoryList.Clear();
            ContentTypesList.Clear();

            try
            {
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();
                await EnsureCategoryContentTypeColumnAsync(connection);

                string typesQuery = "SELECT Id, Name, Name_En, Description, Description_En, IconValue FROM dbo.ContentTypes WHERE IsActive = 1 ORDER BY Name ASC";
                using (var typesCommand = new SqlCommand(typesQuery, connection))
                using (var typeReader = await typesCommand.ExecuteReaderAsync())
                {
                    while (await typeReader.ReadAsync())
                    {
                        ContentTypesList.Add(new ContentTypeFilterDto
                        {
                            Id = typeReader.GetInt64(0),
                            Name = typeReader.GetString(1),
                            Name_En = typeReader.IsDBNull(2) ? null : typeReader.GetString(2),
                            Description = typeReader.IsDBNull(3) ? null : typeReader.GetString(3),
                            Description_En = typeReader.IsDBNull(4) ? null : typeReader.GetString(4),
                            IconValue = typeReader.IsDBNull(5) ? null : typeReader.GetString(5)
                        });
                    }
                }

                string query = "SELECT Id, Name, Name_En, Slug, ParentId, ContentTypeId, IconValue, Image, SortOrder, IsActive, CreatedAt, UpdatedAt FROM dbo.Categories ORDER BY SortOrder ASC, Name ASC";
                using var command = new SqlCommand(query, connection);

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    CategoryList.Add(new CategoryViewModel
                    {
                        Id = reader.GetInt64(0),
                        Name = reader.GetString(1),
                        Name_En = reader.IsDBNull(2) ? null : reader.GetString(2),
                        Slug = reader.IsDBNull(3) ? null : reader.GetString(3),
                        ParentId = reader.IsDBNull(4) ? null : (long?)reader.GetInt64(4),
                        ContentTypeId = reader.IsDBNull(5) ? null : (long?)reader.GetInt64(5),
                        IconValue = reader.IsDBNull(6) ? null : reader.GetString(6),
                        Image = reader.IsDBNull(7) ? null : reader.GetString(7),
                        SortOrder = reader.GetInt32(8),
                        IsActive = reader.GetBoolean(9),
                        CreatedAt = reader.GetDateTime(10),
                        UpdatedAt = reader.IsDBNull(11) ? null : (DateTime?)reader.GetDateTime(11)
                    });
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"خطأ في تحميل الفئات من قاعدة البيانات: {ex.Message}";
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

        private string GenerateSlug(string phrase)
        {
            string str = phrase.ToLowerInvariant();
            // Replace invalid characters with spaces
            str = System.Text.RegularExpressions.Regex.Replace(str, @"[^a-z0-9\s-]", ""); 
            // Convert multiple spaces into one space
            str = System.Text.RegularExpressions.Regex.Replace(str, @"\s+", " ").Trim();
            // Replace spaces with hyphens
            str = System.Text.RegularExpressions.Regex.Replace(str, @"\s", "-"); 
            return str;
        }

        public async Task<IActionResult> OnPostReorderAsync([FromBody] List<ReorderItemDto> items)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return new JsonResult(new { success = false, message = "غير مصرح بالوصول." });
            }

            if (!User.HasClaim("Permission", "Category.Edit"))
            {
                return new JsonResult(new { success = false, message = "ليس لديك صلاحية لتعديل ترتيب التصنيفات." });
            }

            if (items == null || items.Count == 0)
            {
                return new JsonResult(new { success = true, message = "لم يتم تحديد أي تغييرات لحفظها." });
            }

            string? connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                return new JsonResult(new { success = false, message = "جملة الاتصال بقاعدة البيانات غير مهيأة." });
            }

            try
            {
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();
                using var transaction = connection.BeginTransaction();

                try
                {
                    string updateQuery = @"
                        UPDATE dbo.Categories
                        SET ParentId = @ParentId, SortOrder = @SortOrder, UpdatedAt = GETUTCDATE()
                        WHERE Id = @Id";

                    foreach (var item in items)
                    {
                        using var command = new SqlCommand(updateQuery, connection, transaction);
                        command.Parameters.AddWithValue("@Id", item.Id);
                        
                        var dbParentId = (item.ParentId == 0) ? null : item.ParentId;
                        command.Parameters.AddWithValue("@ParentId", (object?)dbParentId ?? DBNull.Value);
                        command.Parameters.AddWithValue("@SortOrder", item.SortOrder);

                        await command.ExecuteNonQueryAsync();
                    }

                    // Query the updated categories list within the transaction
                    var updatedList = new List<CategoryViewModel>();
                    string selectQuery = "SELECT Id, Name, Name_En, Slug, ParentId, ContentTypeId, IconValue, Image, SortOrder, IsActive, CreatedAt, UpdatedAt FROM dbo.Categories ORDER BY SortOrder ASC, Name ASC";
                    using (var selectCmd = new SqlCommand(selectQuery, connection, transaction))
                    {
                        using var reader = await selectCmd.ExecuteReaderAsync();
                        while (await reader.ReadAsync())
                        {
                            updatedList.Add(new CategoryViewModel
                            {
                                Id = reader.GetInt64(0),
                                Name = reader.GetString(1),
                                Name_En = reader.IsDBNull(2) ? null : reader.GetString(2),
                                Slug = reader.IsDBNull(3) ? null : reader.GetString(3),
                                ParentId = reader.IsDBNull(4) ? null : (long?)reader.GetInt64(4),
                                ContentTypeId = reader.IsDBNull(5) ? null : (long?)reader.GetInt64(5),
                                IconValue = reader.IsDBNull(6) ? null : reader.GetString(6),
                                Image = reader.IsDBNull(7) ? null : reader.GetString(7),
                                SortOrder = reader.GetInt32(8),
                                IsActive = reader.GetBoolean(9),
                                CreatedAt = reader.GetDateTime(10),
                                UpdatedAt = reader.IsDBNull(11) ? null : (DateTime?)reader.GetDateTime(11)
                            });
                        }
                    }

                    await transaction.CommitAsync();
                    return new JsonResult(new { success = true, message = "تم تحديث الترتيب بنجاح.", categories = updatedList });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return new JsonResult(new { success = false, message = $"حدث خطأ أثناء تحديث ترتيب الفئات: {ex.Message}" });
                }
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = $"فشل الاتصال بقاعدة البيانات: {ex.Message}" });
            }
        }
    }

    public class ReorderItemDto
    {
        public long Id { get; set; }
        public long? ParentId { get; set; }
        public int SortOrder { get; set; }
    }

    public class CategoryViewModel
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Name_En { get; set; }
        public string? Slug { get; set; }
        public long? ParentId { get; set; }
        public long? ContentTypeId { get; set; }
        public string? IconValue { get; set; }
        public string? Image { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
