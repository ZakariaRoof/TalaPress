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
            await EnsureSettingsBrandingAndCompanyColumnsAsync(connection);
            await EnsureSettingsSmtpColumnsAsync(connection);
            await EnsureContentHitsColumnAsync(connection);
            await EnsureMenuTablesExistAsync(connection);
            await FormsSchemaHelper.EnsureAsync(connectionString);
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

    private static async Task EnsureSettingsBrandingAndCompanyColumnsAsync(SqlConnection connection)
    {
        const string query = @"
            IF OBJECT_ID('dbo.Settings', 'U') IS NOT NULL
            BEGIN
                IF COL_LENGTH('dbo.Settings', 'LogoLight') IS NULL
                    ALTER TABLE dbo.Settings ADD LogoLight NVARCHAR(500) NULL;
                IF COL_LENGTH('dbo.Settings', 'SiteUrl') IS NULL
                    ALTER TABLE dbo.Settings ADD SiteUrl NVARCHAR(500) NULL;
                IF COL_LENGTH('dbo.Settings', 'CompanyMapUrl') IS NULL
                    ALTER TABLE dbo.Settings ADD CompanyMapUrl NVARCHAR(1000) NULL;
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

    private static async Task EnsureSettingsSmtpColumnsAsync(SqlConnection connection)
    {
        const string query = @"
            IF OBJECT_ID('dbo.Settings', 'U') IS NOT NULL
            BEGIN
                IF COL_LENGTH('dbo.Settings', 'SmtpEnabled') IS NULL
                    ALTER TABLE dbo.Settings ADD SmtpEnabled BIT NOT NULL DEFAULT 0;
                IF COL_LENGTH('dbo.Settings', 'SmtpHost') IS NULL
                    ALTER TABLE dbo.Settings ADD SmtpHost NVARCHAR(255) NULL;
                IF COL_LENGTH('dbo.Settings', 'SmtpPort') IS NULL
                    ALTER TABLE dbo.Settings ADD SmtpPort INT NOT NULL DEFAULT 587;
                IF COL_LENGTH('dbo.Settings', 'SmtpUseSsl') IS NULL
                    ALTER TABLE dbo.Settings ADD SmtpUseSsl BIT NOT NULL DEFAULT 1;
                IF COL_LENGTH('dbo.Settings', 'SmtpUsername') IS NULL
                    ALTER TABLE dbo.Settings ADD SmtpUsername NVARCHAR(255) NULL;
                IF COL_LENGTH('dbo.Settings', 'SmtpPasswordProtected') IS NULL
                    ALTER TABLE dbo.Settings ADD SmtpPasswordProtected NVARCHAR(1000) NULL;
                IF COL_LENGTH('dbo.Settings', 'SmtpFromEmail') IS NULL
                    ALTER TABLE dbo.Settings ADD SmtpFromEmail NVARCHAR(255) NULL;
                IF COL_LENGTH('dbo.Settings', 'SmtpFromName') IS NULL
                    ALTER TABLE dbo.Settings ADD SmtpFromName NVARCHAR(255) NULL;
            END";

        await using var command = new SqlCommand(query, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task EnsureFormsTablesExistAsync(SqlConnection connection)
    {
        const string createForms = @"
            IF OBJECT_ID('dbo.Forms', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.Forms (
                    Id BIGINT IDENTITY(1,1) NOT NULL,
                    Name NVARCHAR(255) NOT NULL,
                    Name_En NVARCHAR(255) NULL,
                    Description NVARCHAR(MAX) NULL,
                    Description_En NVARCHAR(MAX) NULL,
                    SubmitButtonText NVARCHAR(100) NULL,
                    SubmitButtonText_En NVARCHAR(100) NULL,
                    SuccessMessage NVARCHAR(500) NULL,
                    SuccessMessage_En NVARCHAR(500) NULL,
                    SendEmailNotification BIT NOT NULL DEFAULT 0,
                    NotificationEmail NVARCHAR(255) NULL,
                    ResponseStorageType NVARCHAR(20) NOT NULL DEFAULT 'Database',
                    IsActive BIT NOT NULL DEFAULT 1,
                    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    UpdatedAt DATETIME2 NULL,
                    CONSTRAINT PK_Forms PRIMARY KEY CLUSTERED (Id)
                );
            END";

        const string createFields = @"
            IF OBJECT_ID('dbo.FormFields', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.FormFields (
                    Id BIGINT IDENTITY(1,1) NOT NULL,
                    FormId BIGINT NOT NULL,
                    FieldName NVARCHAR(100) NOT NULL,
                    Label NVARCHAR(255) NOT NULL,
                    Label_En NVARCHAR(255) NULL,
                    FieldType NVARCHAR(50) NOT NULL DEFAULT 'Text',
                    Placeholder NVARCHAR(255) NULL,
                    Placeholder_En NVARCHAR(255) NULL,
                    HelpText NVARCHAR(500) NULL,
                    HelpText_En NVARCHAR(500) NULL,
                    IsRequired BIT NOT NULL DEFAULT 0,
                    DefaultValue NVARCHAR(500) NULL,
                    OptionsJson NVARCHAR(MAX) NULL,
                    SortOrder INT NOT NULL DEFAULT 0,
                    IsActive BIT NOT NULL DEFAULT 1,
                    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    CONSTRAINT PK_FormFields PRIMARY KEY CLUSTERED (Id),
                    CONSTRAINT FK_FormFields_Forms FOREIGN KEY (FormId) REFERENCES dbo.Forms(Id) ON DELETE CASCADE
                );
                CREATE INDEX IX_FormFields_FormId ON dbo.FormFields(FormId);
            END";

        const string createSubmissions = @"
            IF OBJECT_ID('dbo.FormSubmissions', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.FormSubmissions (
                    Id BIGINT IDENTITY(1,1) NOT NULL,
                    FormId BIGINT NOT NULL,
                    SubmittedDataJson NVARCHAR(MAX) NOT NULL,
                    IpAddress NVARCHAR(64) NULL,
                    UserAgent NVARCHAR(500) NULL,
                    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    CONSTRAINT PK_FormSubmissions PRIMARY KEY CLUSTERED (Id),
                    CONSTRAINT FK_FormSubmissions_Forms FOREIGN KEY (FormId) REFERENCES dbo.Forms(Id) ON DELETE CASCADE
                );
                CREATE INDEX IX_FormSubmissions_FormId ON dbo.FormSubmissions(FormId, CreatedAt DESC);
            END";

        const string permissions = @"
            IF NOT EXISTS (SELECT 1 FROM dbo.Permissions WHERE Code = 'Forms.View')
            BEGIN
                INSERT INTO dbo.Permissions (Code, Name, Name_En, Description, Description_En)
                VALUES
                ('Forms.View', N'عرض النماذج', N'View Forms', N'عرض وإدارة النماذج', N'View and manage forms.'),
                ('Forms.Create', N'إنشاء نموذج', N'Create Form', N'إنشاء نماذج جديدة', N'Create new forms.'),
                ('Forms.Edit', N'تعديل النماذج', N'Edit Forms', N'تعديل النماذج وحقولها', N'Edit forms and fields.'),
                ('Forms.Delete', N'حذف النماذج', N'Delete Forms', N'حذف النماذج', N'Delete forms.'),
                ('FormSubmissions.View', N'عرض ردود النماذج', N'View Submissions', N'عرض ردود النماذج', N'View form submissions.');

                DECLARE @SuperAdminId BIGINT = (SELECT Id FROM dbo.Roles WHERE Name_En = N'Super Administrator');
                IF @SuperAdminId IS NOT NULL
                BEGIN
                    INSERT INTO dbo.RolePermissions (RoleId, PermissionId)
                    SELECT @SuperAdminId, Id FROM dbo.Permissions WHERE Code LIKE 'Forms.%' OR Code = 'FormSubmissions.View';
                END
            END";

        foreach (var sql in new[] { createForms, createFields, createSubmissions, permissions })
        {
            await using var cmd = new SqlCommand(sql, connection);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private static async Task EnsureFormsResponseStorageTypeColumnAsync(SqlConnection connection)
    {
        await FormsSchemaHelper.EnsureAsync(connection.ConnectionString
            ?? throw new InvalidOperationException("Connection string is required."));
    }
}
