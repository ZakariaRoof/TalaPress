using Microsoft.Data.SqlClient;

namespace TalaPress.Infrastructure;

/// <summary>
/// Ensures Forms-related tables and columns exist. Safe to call on each Forms page load.
/// </summary>
public static class FormsSchemaHelper
{
    public static async Task EnsureAsync(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await EnsureFormsTablesAsync(connection);
        await EnsureResponseStorageTypeColumnAsync(connection);
    }

    private static async Task ExecuteNonQueryAsync(SqlConnection connection, string sql)
    {
        await using var cmd = new SqlCommand(sql, connection);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task EnsureFormsTablesAsync(SqlConnection connection)
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
                    ResponseStorageType NVARCHAR(20) NOT NULL CONSTRAINT DF_Forms_ResponseStorageType DEFAULT 'Database',
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

        foreach (var sql in new[] { createForms, createFields, createSubmissions })
        {
            await ExecuteNonQueryAsync(connection, sql);
        }

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

        await ExecuteNonQueryAsync(connection, permissions);
    }

    private static async Task EnsureResponseStorageTypeColumnAsync(SqlConnection connection)
    {
        // IMPORTANT: ALTER and UPDATE must be separate batches — SQL Server validates
        // the whole batch before execution, causing "Invalid column name" if combined.

        const string addColumn = @"
            IF OBJECT_ID('dbo.Forms', 'U') IS NOT NULL
               AND COL_LENGTH('dbo.Forms', 'ResponseStorageType') IS NULL
            BEGIN
                ALTER TABLE dbo.Forms ADD ResponseStorageType NVARCHAR(20) NOT NULL
                    CONSTRAINT DF_Forms_ResponseStorageType_Mig DEFAULT 'Database';
            END";

        await ExecuteNonQueryAsync(connection, addColumn);

        const string backfillEmailForms = @"
            IF OBJECT_ID('dbo.Forms', 'U') IS NOT NULL
               AND COL_LENGTH('dbo.Forms', 'ResponseStorageType') IS NOT NULL
               AND COL_LENGTH('dbo.Forms', 'SendEmailNotification') IS NOT NULL
            BEGIN
                UPDATE dbo.Forms SET ResponseStorageType = 'Both' WHERE SendEmailNotification = 1;
            END";

        await ExecuteNonQueryAsync(connection, backfillEmailForms);
    }
}
