using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TalaPress.Pages
{
    public class MenusModel : PageModel
    {
        private readonly IConfiguration _configuration;

        public MenusModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // Available menus for selection dropdown
        public List<MenuViewModel> MenusList { get; set; } = new();

        // Flat list of menu items for the selected menu
        public List<MenuItemViewModel> MenuItemsList { get; set; } = new();

        // Active Content Types for dropdown binding
        public List<ContentTypeFilterDto> ContentTypesList { get; set; } = new();

        // Active Categories for dropdown binding
        public List<CategoryFilterDto> CategoriesList { get; set; } = new();

        // Current selected menu ID
        public long? SelectedMenuId { get; set; }
        public MenuViewModel? SelectedMenu { get; set; }

        // --- Bind Properties for Menu Item Form ---
        [BindProperty]
        public long Id { get; set; } // Menu Item ID

        [BindProperty]
        public long MenuId { get; set; } // Parent Menu ID

        [BindProperty]
        public long? ParentId { get; set; } // Parent Menu Item ID

        [BindProperty]
        public string Title { get; set; } = string.Empty;

        [BindProperty]
        public string? Title_En { get; set; }

        [BindProperty]
        public new string? Url { get; set; }

        [BindProperty]
        public long? ContentTypeId { get; set; }

        [BindProperty]
        public long? CategoryId { get; set; }

        [BindProperty]
        public long? ContentId { get; set; }

        [BindProperty]
        public string? IconValue { get; set; }

        [BindProperty]
        public int SortOrder { get; set; }

        [BindProperty]
        public bool IsActive { get; set; } = true;

        // --- Bind Properties for Menu Settings Form ---
        [BindProperty]
        public long ManageMenuId { get; set; }

        [BindProperty]
        public string ManageMenuName { get; set; } = string.Empty;

        [BindProperty]
        public string? ManageMenuName_En { get; set; }

        [BindProperty]
        public string ManageMenuCode { get; set; } = string.Empty;

        [BindProperty]
        public bool ManageMenuIsActive { get; set; } = true;

        // Toast feedback messages
        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(long? menuId)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return RedirectToPage("/Login");
            }

            if (!User.HasClaim("Permission", "Menu.View"))
            {
                TempData["ErrorMessage"] = "ليس لديك صلاحية كافية لعرض صفحة إدارة القوائم.";
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
                
                // Initialize database tables and permissions
                await EnsureMenuTablesExistAsync(connection);

                // Load all Menus
                await LoadMenusAsync(connection);

                if (menuId.HasValue)
                {
                    SelectedMenuId = menuId.Value;
                    SelectedMenu = MenusList.Find(m => m.Id == SelectedMenuId.Value);
                }
                else if (MenusList.Count > 0)
                {
                    SelectedMenuId = MenusList[0].Id;
                    SelectedMenu = MenusList[0];
                }

                if (SelectedMenuId.HasValue && SelectedMenu != null)
                {
                    await LoadMenuItemsAsync(connection, SelectedMenuId.Value);
                    await LoadMetadataAsync(connection);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"حدث خطأ أثناء تحميل البيانات: {ex.Message}";
            }

            return Page();
        }

        public async Task<IActionResult> OnPostSaveMenuAsync()
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return RedirectToPage("/Login");
            }

            bool canSave = (ManageMenuId == 0)
                ? User.HasClaim("Permission", "Menu.Create")
                : User.HasClaim("Permission", "Menu.Edit");

            if (!canSave)
            {
                TempData["ErrorMessage"] = "ليس لديك صلاحية كافية لإضافة أو تعديل القوائم.";
                return RedirectToPage("/Menus");
            }

            if (string.IsNullOrWhiteSpace(ManageMenuName))
            {
                TempData["ErrorMessage"] = "اسم القائمة باللغة العربية مطلوب.";
                return RedirectToPage("/Menus");
            }

            if (string.IsNullOrWhiteSpace(ManageMenuCode))
            {
                string baseText = !string.IsNullOrWhiteSpace(ManageMenuName_En) ? ManageMenuName_En : ManageMenuName;
                ManageMenuCode = GenerateCode(baseText);
            }
            else
            {
                ManageMenuCode = GenerateCode(ManageMenuCode);
            }

            string? connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                TempData["ErrorMessage"] = "جملة الاتصال بقاعدة البيانات غير مهيأة.";
                return RedirectToPage("/Menus");
            }

            try
            {
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                if (ManageMenuId == 0)
                {
                    // Check duplicate Code
                    string checkCode = "SELECT COUNT(*) FROM dbo.Menus WHERE Code = @Code";
                    using (var checkCmd = new SqlCommand(checkCode, connection))
                    {
                        checkCmd.Parameters.AddWithValue("@Code", ManageMenuCode);
                        if (Convert.ToInt32(await checkCmd.ExecuteScalarAsync()) > 0)
                        {
                            TempData["ErrorMessage"] = "كود القائمة هذا مستخدم بالفعل. يرجى اختيار اسم آخر.";
                            return RedirectToPage("/Menus");
                        }
                    }

                    // Insert
                    string insertQuery = @"
                        INSERT INTO dbo.Menus (Name, Name_En, Code, IsActive, CreatedAt)
                        VALUES (@Name, @Name_En, @Code, @IsActive, GETUTCDATE());
                        SELECT SCOPE_IDENTITY();";

                    using var command = new SqlCommand(insertQuery, connection);
                    command.Parameters.AddWithValue("@Name", ManageMenuName);
                    command.Parameters.AddWithValue("@Name_En", (object?)ManageMenuName_En ?? DBNull.Value);
                    command.Parameters.AddWithValue("@Code", ManageMenuCode);
                    command.Parameters.AddWithValue("@IsActive", ManageMenuIsActive);

                    long newId = Convert.ToInt64(await command.ExecuteScalarAsync());
                    TempData["SuccessMessage"] = "تم إضافة القائمة الجديدة بنجاح.";
                    return RedirectToPage("/Menus", new { menuId = newId });
                }
                else
                {
                    // Check duplicate Code for other menus
                    string checkCode = "SELECT COUNT(*) FROM dbo.Menus WHERE Code = @Code AND Id <> @Id";
                    using (var checkCmd = new SqlCommand(checkCode, connection))
                    {
                        checkCmd.Parameters.AddWithValue("@Code", ManageMenuCode);
                        checkCmd.Parameters.AddWithValue("@Id", ManageMenuId);
                        if (Convert.ToInt32(await checkCmd.ExecuteScalarAsync()) > 0)
                        {
                            TempData["ErrorMessage"] = "كود القائمة هذا مستخدم بالفعل في قائمة أخرى.";
                            return RedirectToPage("/Menus", new { menuId = ManageMenuId });
                        }
                    }

                    // Update
                    string updateQuery = @"
                        UPDATE dbo.Menus 
                        SET Name = @Name, Name_En = @Name_En, Code = @Code, IsActive = @IsActive, UpdatedAt = GETUTCDATE()
                        WHERE Id = @Id";

                    using var command = new SqlCommand(updateQuery, connection);
                    command.Parameters.AddWithValue("@Id", ManageMenuId);
                    command.Parameters.AddWithValue("@Name", ManageMenuName);
                    command.Parameters.AddWithValue("@Name_En", (object?)ManageMenuName_En ?? DBNull.Value);
                    command.Parameters.AddWithValue("@Code", ManageMenuCode);
                    command.Parameters.AddWithValue("@IsActive", ManageMenuIsActive);

                    await command.ExecuteNonQueryAsync();
                    TempData["SuccessMessage"] = "تم تحديث إعدادات القائمة بنجاح.";
                    return RedirectToPage("/Menus", new { menuId = ManageMenuId });
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"حدث خطأ أثناء حفظ القائمة: {ex.Message}";
            }

            return RedirectToPage("/Menus");
        }

        public async Task<IActionResult> OnPostDeleteMenuAsync(long id)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return RedirectToPage("/Login");
            }

            if (!User.HasClaim("Permission", "Menu.Delete"))
            {
                TempData["ErrorMessage"] = "ليس لديك صلاحية كافية لحذف القوائم.";
                return RedirectToPage("/Menus");
            }

            string? connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                TempData["ErrorMessage"] = "جملة الاتصال بقاعدة البيانات غير مهيأة.";
                return RedirectToPage("/Menus");
            }

            try
            {
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                // Foreign Key CASCADE will delete MenuItems automatically
                string deleteQuery = "DELETE FROM dbo.Menus WHERE Id = @Id";
                using var deleteCmd = new SqlCommand(deleteQuery, connection);
                deleteCmd.Parameters.AddWithValue("@Id", id);
                await deleteCmd.ExecuteNonQueryAsync();

                TempData["SuccessMessage"] = "تم حذف القائمة بنجاح.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"حدث خطأ أثناء حذف القائمة: {ex.Message}";
            }

            return RedirectToPage("/Menus");
        }

        public async Task<IActionResult> OnPostSaveItemAsync()
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return RedirectToPage("/Login");
            }

            bool canSave = (Id == 0)
                ? User.HasClaim("Permission", "Menu.Create")
                : User.HasClaim("Permission", "Menu.Edit");

            if (!canSave)
            {
                TempData["ErrorMessage"] = "ليس لديك صلاحية كافية لحفظ عناصر القائمة.";
                return RedirectToPage("/Menus", new { menuId = MenuId });
            }

            if (string.IsNullOrWhiteSpace(Title))
            {
                TempData["ErrorMessage"] = "عنوان العنصر باللغة العربية مطلوب.";
                return RedirectToPage("/Menus", new { menuId = MenuId });
            }

            // Normalise binding models:
            if (ContentTypeId.HasValue && ContentTypeId.Value <= 0) ContentTypeId = null;
            if (CategoryId.HasValue && CategoryId.Value <= 0) CategoryId = null;
            if (ContentId.HasValue && ContentId.Value <= 0) ContentId = null;

            string? connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                TempData["ErrorMessage"] = "جملة الاتصال بقاعدة البيانات غير مهيأة.";
                return RedirectToPage("/Menus", new { menuId = MenuId });
            }

            try
            {
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                if (Id == 0)
                {
                    // Insert
                    string insertQuery = @"
                        INSERT INTO dbo.MenuItems (MenuId, ParentId, Title, Title_En, Url, ContentTypeId, CategoryId, ContentId, IconValue, SortOrder, IsActive, CreatedAt)
                        VALUES (@MenuId, @ParentId, @Title, @Title_En, @Url, @ContentTypeId, @CategoryId, @ContentId, @IconValue, @SortOrder, @IsActive, GETUTCDATE())";

                    using var command = new SqlCommand(insertQuery, connection);
                    command.Parameters.AddWithValue("@MenuId", MenuId);
                    command.Parameters.AddWithValue("@ParentId", (object?)ParentId ?? DBNull.Value);
                    command.Parameters.AddWithValue("@Title", Title);
                    command.Parameters.AddWithValue("@Title_En", (object?)Title_En ?? DBNull.Value);
                    command.Parameters.AddWithValue("@Url", (object?)Url ?? DBNull.Value);
                    command.Parameters.AddWithValue("@ContentTypeId", (object?)ContentTypeId ?? DBNull.Value);
                    command.Parameters.AddWithValue("@CategoryId", (object?)CategoryId ?? DBNull.Value);
                    command.Parameters.AddWithValue("@ContentId", (object?)ContentId ?? DBNull.Value);
                    command.Parameters.AddWithValue("@IconValue", (object?)IconValue ?? DBNull.Value);
                    command.Parameters.AddWithValue("@SortOrder", SortOrder);
                    command.Parameters.AddWithValue("@IsActive", IsActive);

                    await command.ExecuteNonQueryAsync();
                    TempData["SuccessMessage"] = "تم إضافة عنصر القائمة بنجاح.";
                }
                else
                {
                    // Update
                    if (ParentId == Id)
                    {
                        TempData["ErrorMessage"] = "لا يمكن ربط العنصر بنفسه كأب.";
                        return RedirectToPage("/Menus", new { menuId = MenuId });
                    }

                    string updateQuery = @"
                        UPDATE dbo.MenuItems 
                        SET ParentId = @ParentId, Title = @Title, Title_En = @Title_En, Url = @Url, 
                            ContentTypeId = @ContentTypeId, CategoryId = @CategoryId, ContentId = @ContentId, 
                            IconValue = @IconValue, SortOrder = @SortOrder, IsActive = @IsActive, UpdatedAt = GETUTCDATE()
                        WHERE Id = @Id";

                    using var command = new SqlCommand(updateQuery, connection);
                    command.Parameters.AddWithValue("@Id", Id);
                    command.Parameters.AddWithValue("@ParentId", (object?)ParentId ?? DBNull.Value);
                    command.Parameters.AddWithValue("@Title", Title);
                    command.Parameters.AddWithValue("@Title_En", (object?)Title_En ?? DBNull.Value);
                    command.Parameters.AddWithValue("@Url", (object?)Url ?? DBNull.Value);
                    command.Parameters.AddWithValue("@ContentTypeId", (object?)ContentTypeId ?? DBNull.Value);
                    command.Parameters.AddWithValue("@CategoryId", (object?)CategoryId ?? DBNull.Value);
                    command.Parameters.AddWithValue("@ContentId", (object?)ContentId ?? DBNull.Value);
                    command.Parameters.AddWithValue("@IconValue", (object?)IconValue ?? DBNull.Value);
                    command.Parameters.AddWithValue("@SortOrder", SortOrder);
                    command.Parameters.AddWithValue("@IsActive", IsActive);

                    await command.ExecuteNonQueryAsync();
                    TempData["SuccessMessage"] = "تم تحديث عنصر القائمة بنجاح.";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"حدث خطأ أثناء حفظ عنصر القائمة: {ex.Message}";
            }

            return RedirectToPage("/Menus", new { menuId = MenuId });
        }

        public async Task<IActionResult> OnPostDeleteItemAsync(long id, long menuId)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return RedirectToPage("/Login");
            }

            if (!User.HasClaim("Permission", "Menu.Delete"))
            {
                TempData["ErrorMessage"] = "ليس لديك صلاحية كافية لحذف عناصر القوائم.";
                return RedirectToPage("/Menus", new { menuId = menuId });
            }

            string? connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                TempData["ErrorMessage"] = "جملة الاتصال بقاعدة البيانات غير مهيأة.";
                return RedirectToPage("/Menus", new { menuId = menuId });
            }

            try
            {
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                // Check for children
                string checkQuery = "SELECT COUNT(*) FROM dbo.MenuItems WHERE ParentId = @Id";
                using (var checkCmd = new SqlCommand(checkQuery, connection))
                {
                    checkCmd.Parameters.AddWithValue("@Id", id);
                    if (Convert.ToInt32(await checkCmd.ExecuteScalarAsync()) > 0)
                    {
                        TempData["ErrorMessage"] = "لا يمكن حذف هذا العنصر لأنه يحتوي على عناصر فرعية. يرجى حذفها أو إعادة نقلها أولاً.";
                        return RedirectToPage("/Menus", new { menuId = menuId });
                    }
                }

                string deleteQuery = "DELETE FROM dbo.MenuItems WHERE Id = @Id";
                using var deleteCmd = new SqlCommand(deleteQuery, connection);
                deleteCmd.Parameters.AddWithValue("@Id", id);
                await deleteCmd.ExecuteNonQueryAsync();

                TempData["SuccessMessage"] = "تم حذف عنصر القائمة بنجاح.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"حدث خطأ أثناء حذف عنصر القائمة: {ex.Message}";
            }

            return RedirectToPage("/Menus", new { menuId = menuId });
        }

        public async Task<IActionResult> OnPostReorderAsync([FromBody] List<ReorderItemDto> items)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return new JsonResult(new { success = false, message = "غير مصرح بالوصول." });
            }

            if (!User.HasClaim("Permission", "Menu.Edit"))
            {
                return new JsonResult(new { success = false, message = "ليس لديك صلاحية لتعديل ترتيب عناصر القائمة." });
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
                        UPDATE dbo.MenuItems
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

                    await transaction.CommitAsync();
                    return new JsonResult(new { success = true, message = "تم حفظ الترتيب الجديد بنجاح." });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return new JsonResult(new { success = false, message = $"حدث خطأ أثناء التحديث: {ex.Message}" });
                }
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = $"فشل الاتصال بقاعدة البيانات: {ex.Message}" });
            }
        }

        public async Task<IActionResult> OnGetSearchContentAsync(long contentTypeId, long? categoryId, string? query)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return new JsonResult(new { error = "Unauthorized" });
            }

            string? connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                return new JsonResult(new { error = "Connection missing" });
            }

            var results = new List<dynamic>();

            try
            {
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                var clauses = new List<string> { "ContentTypeId = @ContentTypeId", "IsDeleted = 0" };
                if (categoryId.HasValue && categoryId.Value > 0)
                {
                    clauses.Add("CategoryId = @CategoryId");
                }

                if (!string.IsNullOrWhiteSpace(query))
                {
                    clauses.Add("(Title LIKE @Query OR Title_En LIKE @Query)");
                }

                string whereStr = string.Join(" AND ", clauses);
                string selectQuery = $@"
                    SELECT TOP 10 Id, Title, Title_En 
                    FROM dbo.Content 
                    WHERE {whereStr} 
                    ORDER BY Title ASC";

                using var cmd = new SqlCommand(selectQuery, connection);
                cmd.Parameters.AddWithValue("@ContentTypeId", contentTypeId);
                if (categoryId.HasValue && categoryId.Value > 0)
                {
                    cmd.Parameters.AddWithValue("@CategoryId", categoryId.Value);
                }
                if (!string.IsNullOrWhiteSpace(query))
                {
                    cmd.Parameters.AddWithValue("@Query", "%" + query + "%");
                }

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    results.Add(new
                    {
                        id = reader.GetInt64(0),
                        title = reader.IsDBNull(1) ? null : reader.GetString(1),
                        title_En = reader.IsDBNull(2) ? null : reader.GetString(2)
                    });
                }
            }
            catch (Exception ex)
            {
                return new JsonResult(new { error = ex.Message });
            }

            return new JsonResult(results);
        }

        // --- Data Loading & Table Initializers ---
        private async Task EnsureMenuTablesExistAsync(SqlConnection connection)
        {
            // 1. Create dbo.Menus
            const string createMenusTable = @"
                IF OBJECT_ID('dbo.Menus', 'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.Menus (
                        Id BIGINT IDENTITY(1,1) NOT NULL,
                        Name NVARCHAR(255) NOT NULL,
                        Name_En NVARCHAR(255) NULL,
                        Code NVARCHAR(100) NOT NULL,
                        IsActive BIT NOT NULL DEFAULT 1,
                        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                        UpdatedAt DATETIME2 NULL,
                        CONSTRAINT PK_Menus PRIMARY KEY CLUSTERED (Id)
                    );
                    CREATE UNIQUE NONCLUSTERED INDEX UQ_Menus_Code ON dbo.Menus(Code);
                END";
            using (var cmd = new SqlCommand(createMenusTable, connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            // 2. Create dbo.MenuItems
            const string createMenuItemsTable = @"
                IF OBJECT_ID('dbo.MenuItems', 'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.MenuItems (
                        Id BIGINT IDENTITY(1,1) NOT NULL,
                        MenuId BIGINT NOT NULL,
                        ParentId BIGINT NULL,
                        Title NVARCHAR(255) NOT NULL,
                        Title_En NVARCHAR(255) NULL,
                        Url NVARCHAR(2048) NULL,
                        ContentTypeId BIGINT NULL,
                        CategoryId BIGINT NULL,
                        ContentId BIGINT NULL,
                        IconValue NVARCHAR(100) NULL,
                        SortOrder INT NOT NULL DEFAULT 0,
                        IsActive BIT NOT NULL DEFAULT 1,
                        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                        UpdatedAt DATETIME2 NULL,
                        CONSTRAINT PK_MenuItems PRIMARY KEY CLUSTERED (Id),
                        CONSTRAINT FK_MenuItems_Menus FOREIGN KEY (MenuId) REFERENCES dbo.Menus(Id) ON DELETE CASCADE,
                        CONSTRAINT FK_MenuItems_ContentTypes FOREIGN KEY (ContentTypeId) REFERENCES dbo.ContentTypes(Id) ON DELETE SET NULL,
                        CONSTRAINT FK_MenuItems_Categories FOREIGN KEY (CategoryId) REFERENCES dbo.Categories(Id) ON DELETE SET NULL
                    );
                    CREATE NONCLUSTERED INDEX IX_MenuItems_MenuId ON dbo.MenuItems(MenuId);
                    CREATE NONCLUSTERED INDEX IX_MenuItems_ParentId ON dbo.MenuItems(ParentId);
                END";
            using (var cmd = new SqlCommand(createMenuItemsTable, connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            // 3. Create Permissions if they don't exist
            const string insertPermissions = @"
                IF NOT EXISTS (SELECT 1 FROM dbo.Permissions WHERE Code = 'Menu.View')
                BEGIN
                    INSERT INTO dbo.Permissions (Code, Name, Name_En, Description, Description_En)
                    VALUES 
                    ('Menu.View', N'عرض القوائم', N'View Menus', N'عرض وإدارة القوائم ونظام الملاحة', N'Allows viewing and managing navigation menus.'),
                    ('Menu.Create', N'إنشاء قائمة جديدة', N'Create Menu', N'إضافة قوائم جديدة وعناصر ملاحة', N'Allows creating new navigation menus.'),
                    ('Menu.Edit', N'تعديل القوائم', N'Edit Menu', N'تعديل وإعادة ترتيب القوائم وعناصرها', N'Allows editing and reordering menus and items.'),
                    ('Menu.Delete', N'حذف القوائم', N'Delete Menu', N'حذف القوائم وعناصر الملاحة نهائياً', N'Allows deleting navigation menus.');

                    -- Automatically assign to Super Administrator
                    DECLARE @SuperAdminId BIGINT = (SELECT Id FROM dbo.Roles WHERE Name_En = N'Super Administrator');
                    IF @SuperAdminId IS NOT NULL
                    BEGIN
                        INSERT INTO dbo.RolePermissions (RoleId, PermissionId)
                        SELECT @SuperAdminId, Id FROM dbo.Permissions WHERE Code LIKE 'Menu.%';
                    END
                END";
            using (var cmd = new SqlCommand(insertPermissions, connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }
        }

        private async Task LoadMenusAsync(SqlConnection connection)
        {
            MenusList.Clear();
            string query = "SELECT Id, Name, Name_En, Code, IsActive, CreatedAt, UpdatedAt FROM dbo.Menus ORDER BY Name ASC";
            using var cmd = new SqlCommand(query, connection);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                MenusList.Add(new MenuViewModel
                {
                    Id = reader.GetInt64(0),
                    Name = reader.GetString(1),
                    Name_En = reader.IsDBNull(2) ? null : reader.GetString(2),
                    Code = reader.GetString(3),
                    IsActive = reader.GetBoolean(4),
                    CreatedAt = reader.GetDateTime(5),
                    UpdatedAt = reader.IsDBNull(6) ? null : (DateTime?)reader.GetDateTime(6)
                });
            }
        }

        private async Task LoadMenuItemsAsync(SqlConnection connection, long menuId)
        {
            MenuItemsList.Clear();
            string query = @"
                SELECT mi.Id, mi.MenuId, mi.ParentId, mi.Title, mi.Title_En, mi.Url, 
                       mi.ContentTypeId, mi.CategoryId, mi.ContentId, mi.IconValue, 
                       mi.SortOrder, mi.IsActive, ct.Name AS ContentTypeName, ct.Name_En AS ContentTypeNameEn,
                       c.Name AS CategoryName, c.Name_En AS CategoryNameEn,
                       co.Title AS ContentTitle, co.Title_En AS ContentTitleEn
                FROM dbo.MenuItems mi
                LEFT JOIN dbo.ContentTypes ct ON mi.ContentTypeId = ct.Id
                LEFT JOIN dbo.Categories c ON mi.CategoryId = c.Id
                LEFT JOIN dbo.Content co ON mi.ContentId = co.Id AND co.IsDeleted = 0
                WHERE mi.MenuId = @MenuId
                ORDER BY mi.SortOrder ASC, mi.Title ASC";

            using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@MenuId", menuId);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                MenuItemsList.Add(new MenuItemViewModel
                {
                    Id = reader.GetInt64(0),
                    MenuId = reader.GetInt64(1),
                    ParentId = reader.IsDBNull(2) ? null : (long?)reader.GetInt64(2),
                    Title = reader.GetString(3),
                    Title_En = reader.IsDBNull(4) ? null : reader.GetString(4),
                    Url = reader.IsDBNull(5) ? null : reader.GetString(5),
                    ContentTypeId = reader.IsDBNull(6) ? null : (long?)reader.GetInt64(6),
                    CategoryId = reader.IsDBNull(7) ? null : (long?)reader.GetInt64(7),
                    ContentId = reader.IsDBNull(8) ? null : (long?)reader.GetInt64(8),
                    IconValue = reader.IsDBNull(9) ? null : reader.GetString(9),
                    SortOrder = reader.GetInt32(10),
                    IsActive = reader.GetBoolean(11),
                    ContentTypeName = reader.IsDBNull(12) ? null : reader.GetString(12),
                    ContentTypeNameEn = reader.IsDBNull(13) ? null : reader.GetString(13),
                    CategoryName = reader.IsDBNull(14) ? null : reader.GetString(14),
                    CategoryNameEn = reader.IsDBNull(15) ? null : reader.GetString(15),
                    ContentTitle = reader.IsDBNull(16) ? null : reader.GetString(16),
                    ContentTitleEn = reader.IsDBNull(17) ? null : reader.GetString(17)
                });
            }
        }

        private async Task LoadMetadataAsync(SqlConnection connection)
        {
            ContentTypesList.Clear();
            CategoriesList.Clear();

            // Load Content Types
            string typesQuery = "SELECT Id, Name, Name_En, Description, Description_En, IconValue FROM dbo.ContentTypes WHERE IsActive = 1 ORDER BY Name ASC";
            using (var cmd = new SqlCommand(typesQuery, connection))
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    ContentTypesList.Add(new ContentTypeFilterDto
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

            // Load Categories
            string catsQuery = "SELECT Id, Name, Name_En, ParentId, ContentTypeId FROM dbo.Categories WHERE IsActive = 1 ORDER BY SortOrder ASC, Name ASC";
            using (var cmd = new SqlCommand(catsQuery, connection))
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

        private string GenerateCode(string text)
        {
            string str = text.ToLowerInvariant();
            str = System.Text.RegularExpressions.Regex.Replace(str, @"[^a-z0-9\s-]", ""); 
            str = System.Text.RegularExpressions.Regex.Replace(str, @"\s+", " ").Trim();
            str = System.Text.RegularExpressions.Regex.Replace(str, @"\s", "-"); 
            return str;
        }
    }

    // --- ViewModel and DTO classes ---
    public class MenuViewModel
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Name_En { get; set; }
        public string Code { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class MenuItemViewModel
    {
        public long Id { get; set; }
        public long MenuId { get; set; }
        public long? ParentId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Title_En { get; set; }
        public string? Url { get; set; }
        public long? ContentTypeId { get; set; }
        public long? CategoryId { get; set; }
        public long? ContentId { get; set; }
        public string? IconValue { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }

        // Mapped values
        public string? ContentTypeName { get; set; }
        public string? ContentTypeNameEn { get; set; }
        public string? CategoryName { get; set; }
        public string? CategoryNameEn { get; set; }
        public string? ContentTitle { get; set; }
        public string? ContentTitleEn { get; set; }
    }
}
