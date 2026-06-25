using Microsoft.Data.SqlClient;

namespace TalaPress.Infrastructure;

public static class DatabaseInitializer
{
    public static async Task EnsureDatabaseUpdatesAsync(IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            await EnsureCategoryContentTypeColumnAsync(connection);
            await EnsureSettingsShowHitsColumnAsync(connection);
            await EnsureContentHitsColumnAsync(connection);
            await EnsureMenuTablesExistAsync(connection);
        }
        catch
        {
            // Keep application startup resilient if the database is unavailable.
        }
    }

    private static async Task EnsureMenuTablesExistAsync(SqlConnection connection)
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
        await using (var cmd = new SqlCommand(createMenusTable, connection))
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
        await using (var cmd = new SqlCommand(createMenuItemsTable, connection))
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
        await using (var cmd = new SqlCommand(insertPermissions, connection))
        {
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private static async Task EnsureCategoryContentTypeColumnAsync(SqlConnection connection)
    {
        const string query = @"
            IF OBJECT_ID('dbo.Categories', 'U') IS NOT NULL
               AND COL_LENGTH('dbo.Categories', 'ContentTypeId') IS NULL
            BEGIN
                ALTER TABLE dbo.Categories ADD ContentTypeId BIGINT NULL;
                CREATE INDEX IX_Categories_ContentTypeId ON dbo.Categories(ContentTypeId, ParentId, SortOrder);
            END";

        await using var command = new SqlCommand(query, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task EnsureSettingsShowHitsColumnAsync(SqlConnection connection)
    {
        const string query = @"
            IF OBJECT_ID('dbo.Settings', 'U') IS NOT NULL
               AND COL_LENGTH('dbo.Settings', 'ShowHits') IS NULL
            BEGIN
                ALTER TABLE dbo.Settings ADD ShowHits BIT NOT NULL DEFAULT 1;
            END";

        await using var command = new SqlCommand(query, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task EnsureContentHitsColumnAsync(SqlConnection connection)
    {
        const string query = @"
            IF OBJECT_ID('dbo.Content', 'U') IS NOT NULL
               AND COL_LENGTH('dbo.Content', 'Hits') IS NULL
            BEGIN
                ALTER TABLE dbo.Content ADD Hits INT NOT NULL DEFAULT 0;
            END";

        await using var command = new SqlCommand(query, connection);
        await command.ExecuteNonQueryAsync();
    }
}
