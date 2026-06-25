using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TalaPress.Pages
{
    public class FormViewModel
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Name_En { get; set; }
        public string? Description { get; set; }
        public string? Description_En { get; set; }
        public string? SubmitButtonText { get; set; }
        public string? SubmitButtonText_En { get; set; }
        public string? SuccessMessage { get; set; }
        public string? SuccessMessage_En { get; set; }
        public bool SendEmailNotification { get; set; }
        public string? NotificationEmail { get; set; }
        public bool IsActive { get; set; }
    }

    public partial class FormFieldDto
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
        public string? DefaultValue { get; set; }
        public string? OptionsJson { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class FormsModel : PageModel
    {
        private readonly IConfiguration _configuration;

        public FormsModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public List<FormViewModel> FormsList { get; set; } = new();
        public List<DatabaseTableDto> DatabaseTables { get; set; } = new();
        public List<DatabaseColumnDto> DatabaseColumns { get; set; } = new();

        public long SelectedFormId { get; set; }

        [BindProperty]
        public long Id { get; set; }

        [BindProperty]
        public string Name { get; set; } = string.Empty;

        [BindProperty]
        public string? Name_En { get; set; }

        [BindProperty]
        public string? Description { get; set; }

        [BindProperty]
        public string? Description_En { get; set; }

        [BindProperty]
        public string? SubmitButtonText { get; set; }

        [BindProperty]
        public string? SubmitButtonText_En { get; set; }

        [BindProperty]
        public string? SuccessMessage { get; set; }

        [BindProperty]
        public string? SuccessMessage_En { get; set; }

        [BindProperty]
        public bool SendEmailNotification { get; set; }

        [BindProperty]
        public string? NotificationEmail { get; set; }

        [BindProperty]
        public bool IsActive { get; set; } = true;

        [BindProperty]
        public string CustomFieldsJson { get; set; } = "[]";

        [TempData]
        public string? SuccessMessageAlert { get; set; }

        [TempData]
        public string? ErrorMessageAlert { get; set; }

        public async Task<IActionResult> OnGetAsync(long? selectedId)
        {
            if (User.Identity?.IsAuthenticated != true) return RedirectToPage("/Login");
            if (!User.HasClaim("Permission", "Forms.View"))
            {
                TempData["ErrorMessage"] = "ليس لديك صلاحية كافية لعرض النماذج.";
                return RedirectToPage("/Index");
            }

            string? connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString)) return Page();

            await LoadFormsListAsync(connectionString);
            await LoadDatabaseSchemaOptionsAsync(connectionString);

            if (selectedId.HasValue && selectedId.Value > 0)
            {
                SelectedFormId = selectedId.Value;
                var currentForm = FormsList.FirstOrDefault(f => f.Id == SelectedFormId);
                if (currentForm != null)
                {
                    Id = currentForm.Id;
                    Name = currentForm.Name;
                    Name_En = currentForm.Name_En;
                    Description = currentForm.Description;
                    Description_En = currentForm.Description_En;
                    SubmitButtonText = currentForm.SubmitButtonText;
                    SubmitButtonText_En = currentForm.SubmitButtonText_En;
                    SuccessMessage = currentForm.SuccessMessage;
                    SuccessMessage_En = currentForm.SuccessMessage_En;
                    SendEmailNotification = currentForm.SendEmailNotification;
                    NotificationEmail = currentForm.NotificationEmail;
                    IsActive = currentForm.IsActive;

                    var fields = await GetFormFieldsAsync(connectionString, Id);
                    CustomFieldsJson = JsonSerializer.Serialize(fields);
                }
            }
            else
            {
                Id = 0;
                Name = string.Empty;
                Name_En = string.Empty;
                IsActive = true;
                CustomFieldsJson = "[]";
            }

            return Page();
        }

        public async Task<IActionResult> OnPostSaveAsync()
        {
            if (User.Identity?.IsAuthenticated != true) return RedirectToPage("/Login");
            bool canSave = Id == 0 ? User.HasClaim("Permission", "Forms.Create") : User.HasClaim("Permission", "Forms.Edit");
            if (!canSave)
            {
                TempData["ErrorMessage"] = "ليس لديك صلاحية لحفظ النماذج.";
                return RedirectToPage("/Index");
            }

            if (string.IsNullOrWhiteSpace(Name))
            {
                ErrorMessageAlert = "الاسم بالعربية مطلوب.";
                return await OnGetReloadAsync();
            }

            List<FormFieldDto> formFields;
            try
            {
                formFields = JsonSerializer.Deserialize<List<FormFieldDto>>(CustomFieldsJson) ?? new();
            }
            catch (Exception ex)
            {
                ErrorMessageAlert = $"خطأ في بيانات الحقول: {ex.Message}";
                return await OnGetReloadAsync();
            }

            // Check if there are any fields
            if (formFields.Count == 0)
            {
                ErrorMessageAlert = "يجب إضافة حقل واحد على الأقل قبل حفظ النموذج.";
                return await OnGetReloadAsync();
            }

            var processedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var field in formFields)
            {
                if (string.IsNullOrWhiteSpace(field.FieldName) || string.IsNullOrWhiteSpace(field.Label))
                {
                    ErrorMessageAlert = "الاسم البرمجي للحقل والتسمية مطلوبان.";
                    return await OnGetReloadAsync();
                }
                if (!Regex.IsMatch(field.FieldName, "^[A-Za-z][A-Za-z0-9_]*$"))
                {
                    ErrorMessageAlert = $"اسم الحقل '{field.FieldName}' غير صحيح. يجب أن يبدأ بحرف ويحتوي على أحرف وأرقام وشرطة سفلية فقط.";
                    return await OnGetReloadAsync();
                }
                if (!processedNames.Add(field.FieldName))
                {
                    ErrorMessageAlert = $"الحقل '{field.FieldName}' مكرر. استخدم أسماء برمجية فريدة.";
                    return await OnGetReloadAsync();
                }
            }

            string? connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                ErrorMessageAlert = "فشل الاتصال بقاعدة البيانات: سلسلة الاتصال غير موجودة.";
                return await OnGetReloadAsync();
            }

            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            SqlTransaction? transaction = null;
            try
            {
                transaction = connection.BeginTransaction();
                long formId = Id;
                if (formId == 0)
                {
                    string insertQuery = @"
                        INSERT INTO dbo.Forms (Name, Name_En, Description, Description_En, SubmitButtonText, SubmitButtonText_En, SuccessMessage, SuccessMessage_En, SendEmailNotification, NotificationEmail, IsActive, CreatedAt)
                        VALUES (@Name, @Name_En, @Description, @Description_En, @SubmitButtonText, @SubmitButtonText_En, @SuccessMessage, @SuccessMessage_En, @SendEmailNotification, @NotificationEmail, @IsActive, GETUTCDATE());
                        SELECT SCOPE_IDENTITY();";
                    using var cmd = new SqlCommand(insertQuery, connection, transaction);
                    cmd.Parameters.AddWithValue("@Name", Name);
                    cmd.Parameters.AddWithValue("@Name_En", (object?)Name_En ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Description", (object?)Description ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Description_En", (object?)Description_En ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@SubmitButtonText", (object?)SubmitButtonText ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@SubmitButtonText_En", (object?)SubmitButtonText_En ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@SuccessMessage", (object?)SuccessMessage ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@SuccessMessage_En", (object?)SuccessMessage_En ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@SendEmailNotification", SendEmailNotification);
                    cmd.Parameters.AddWithValue("@NotificationEmail", (object?)NotificationEmail ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@IsActive", IsActive);
                    formId = Convert.ToInt64(await cmd.ExecuteScalarAsync());
                    SuccessMessageAlert = "تم إضافة النموذج بنجاح.";
                }
                else
                {
                    string updateQuery = @"
                        UPDATE dbo.Forms SET Name=@Name, Name_En=@Name_En, Description=@Description, Description_En=@Description_En, 
                        SubmitButtonText=@SubmitButtonText, SubmitButtonText_En=@SubmitButtonText_En, SuccessMessage=@SuccessMessage, SuccessMessage_En=@SuccessMessage_En, 
                        SendEmailNotification=@SendEmailNotification, NotificationEmail=@NotificationEmail, IsActive=@IsActive, UpdatedAt=GETUTCDATE()
                        WHERE Id=@Id";
                    using var cmd = new SqlCommand(updateQuery, connection, transaction);
                    cmd.Parameters.AddWithValue("@Id", formId);
                    cmd.Parameters.AddWithValue("@Name", Name);
                    cmd.Parameters.AddWithValue("@Name_En", (object?)Name_En ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Description", (object?)Description ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Description_En", (object?)Description_En ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@SubmitButtonText", (object?)SubmitButtonText ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@SubmitButtonText_En", (object?)SubmitButtonText_En ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@SuccessMessage", (object?)SuccessMessage ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@SuccessMessage_En", (object?)SuccessMessage_En ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@SendEmailNotification", SendEmailNotification);
                    cmd.Parameters.AddWithValue("@NotificationEmail", (object?)NotificationEmail ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@IsActive", IsActive);
                    await cmd.ExecuteNonQueryAsync();
                    SuccessMessageAlert = "تم تحديث النموذج بنجاح.";
                }

                // Sync Fields
                string deleteFields = "DELETE FROM dbo.FormFields WHERE FormId = @FormId";
                using var delCmd = new SqlCommand(deleteFields, connection, transaction);
                delCmd.Parameters.AddWithValue("@FormId", formId);
                await delCmd.ExecuteNonQueryAsync();

                string insertField = @"
                    INSERT INTO dbo.FormFields (FormId, FieldName, Label, Label_En, FieldType, Placeholder, Placeholder_En, HelpText, HelpText_En, IsRequired, DefaultValue, OptionsJson, SortOrder, IsActive, CreatedAt)
                    VALUES (@FormId, @FieldName, @Label, @Label_En, @FieldType, @Placeholder, @Placeholder_En, @HelpText, @HelpText_En, @IsRequired, @DefaultValue, @OptionsJson, @SortOrder, @IsActive, GETUTCDATE())";

                for (int i = 0; i < formFields.Count; i++)
                {
                    var f = formFields[i];
                    using var fCmd = new SqlCommand(insertField, connection, transaction);
                    fCmd.Parameters.AddWithValue("@FormId", formId);
                    fCmd.Parameters.AddWithValue("@FieldName", f.FieldName);
                    fCmd.Parameters.AddWithValue("@Label", f.Label);
                    fCmd.Parameters.AddWithValue("@Label_En", (object?)f.Label_En ?? DBNull.Value);
                    fCmd.Parameters.AddWithValue("@FieldType", f.FieldType);
                    fCmd.Parameters.AddWithValue("@Placeholder", (object?)f.Placeholder ?? DBNull.Value);
                    fCmd.Parameters.AddWithValue("@Placeholder_En", (object?)f.Placeholder_En ?? DBNull.Value);
                    fCmd.Parameters.AddWithValue("@HelpText", (object?)f.HelpText ?? DBNull.Value);
                    fCmd.Parameters.AddWithValue("@HelpText_En", (object?)f.HelpText_En ?? DBNull.Value);
                    fCmd.Parameters.AddWithValue("@IsRequired", f.IsRequired);
                    fCmd.Parameters.AddWithValue("@DefaultValue", (object?)f.DefaultValue ?? DBNull.Value);
                    fCmd.Parameters.AddWithValue("@OptionsJson", (object?)f.OptionsJson ?? DBNull.Value);
                    fCmd.Parameters.AddWithValue("@SortOrder", i + 1);
                    fCmd.Parameters.AddWithValue("@IsActive", f.IsActive);
                    await fCmd.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
                Id = formId;
                SuccessMessageAlert = Id == 0 
                    ? "تم إضافة النموذج بنجاح." 
                    : "تم تحديث النموذج وحقوله بنجاح.";
            }
            catch (Exception ex)
            {
                if (transaction != null)
                {
                    await transaction.RollbackAsync();
                }
                ErrorMessageAlert = $"فشل الحفظ: {ex.Message}";
                return await OnGetReloadAsync();
            }
            finally
            {
                transaction?.Dispose();
            }

            return RedirectToPage(new { selectedId = Id });
        }

        public async Task<IActionResult> OnPostDeleteAsync(long id)
        {
            if (User.Identity?.IsAuthenticated != true) return RedirectToPage("/Login");
            if (!User.HasClaim("Permission", "Forms.Delete"))
            {
                TempData["ErrorMessage"] = "ليس لديك صلاحية الحذف.";
                return RedirectToPage("/Index");
            }

            if (id <= 0)
            {
                TempData["ErrorMessage"] = "معرف النموذج غير صحيح.";
                return RedirectToPage(new { selectedId = (long?)null });
            }

            string? connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                TempData["ErrorMessage"] = "فشل الحذف: لم يتمكن من الاتصال بقاعدة البيانات.";
                return RedirectToPage(new { selectedId = (long?)null });
            }

            using var connection = new SqlConnection(connectionString);
            using var transaction = connection.BeginTransaction();
            try
            {
                await connection.OpenAsync();
                using (var cmd = new SqlCommand("DELETE FROM dbo.FormFields WHERE FormId=@Id", connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    await cmd.ExecuteNonQueryAsync();
                }
                using (var cmd = new SqlCommand("DELETE FROM dbo.Forms WHERE Id=@Id", connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    var result = await cmd.ExecuteNonQueryAsync();
                    if (result == 0)
                    {
                        await transaction.RollbackAsync();
                        TempData["ErrorMessage"] = "لم يتم العثور على النموذج المراد حذفه.";
                        return RedirectToPage(new { selectedId = (long?)null });
                    }
                }
                await transaction.CommitAsync();
                SuccessMessageAlert = "تم حذف النموذج وجميع حقوله بنجاح.";
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                ErrorMessageAlert = $"فشل الحذف: {ex.Message}";
            }

            return RedirectToPage(new { selectedId = (long?)null });
        }

        private async Task LoadFormsListAsync(string connectionString)
        {
            FormsList.Clear();
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            using var command = new SqlCommand("SELECT Id, Name, Name_En, Description, Description_En, SubmitButtonText, SubmitButtonText_En, SuccessMessage, SuccessMessage_En, SendEmailNotification, NotificationEmail, IsActive FROM dbo.Forms ORDER BY CreatedAt DESC", connection);
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                FormsList.Add(new FormViewModel
                {
                    Id = reader.GetInt64(0),
                    Name = reader.GetString(1),
                    Name_En = reader.IsDBNull(2) ? null : reader.GetString(2),
                    Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Description_En = reader.IsDBNull(4) ? null : reader.GetString(4),
                    SubmitButtonText = reader.IsDBNull(5) ? null : reader.GetString(5),
                    SubmitButtonText_En = reader.IsDBNull(6) ? null : reader.GetString(6),
                    SuccessMessage = reader.IsDBNull(7) ? null : reader.GetString(7),
                    SuccessMessage_En = reader.IsDBNull(8) ? null : reader.GetString(8),
                    SendEmailNotification = reader.GetBoolean(9),
                    NotificationEmail = reader.IsDBNull(10) ? null : reader.GetString(10),
                    IsActive = reader.GetBoolean(11)
                });
            }
        }

        private async Task<List<FormFieldDto>> GetFormFieldsAsync(string connectionString, long formId)
        {
            var list = new List<FormFieldDto>();
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            using var command = new SqlCommand("SELECT Id, FieldName, Label, Label_En, FieldType, Placeholder, Placeholder_En, HelpText, HelpText_En, IsRequired, DefaultValue, OptionsJson, SortOrder, IsActive FROM dbo.FormFields WHERE FormId=@FormId ORDER BY SortOrder", connection);
            command.Parameters.AddWithValue("@FormId", formId);
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new FormFieldDto
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
                    DefaultValue = reader.IsDBNull(10) ? null : reader.GetString(10),
                    OptionsJson = reader.IsDBNull(11) ? null : reader.GetString(11),
                    SortOrder = reader.GetInt32(12),
                    IsActive = reader.GetBoolean(13)
                });
            }
            return list;
        }

        private async Task LoadDatabaseSchemaOptionsAsync(string connectionString)
        {
            DatabaseTables.Clear();
            DatabaseColumns.Clear();
            try
            {
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                string tablesQuery = @"
                    SELECT t.name AS TableName
                    FROM sys.tables t
                    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                    WHERE s.name = 'dbo' AND t.name NOT IN ('sysdiagrams')
                    ORDER BY t.name";

                using var tCmd = new SqlCommand(tablesQuery, connection);
                using var tReader = await tCmd.ExecuteReaderAsync();
                while (await tReader.ReadAsync())
                {
                    DatabaseTables.Add(new DatabaseTableDto { Name = tReader.GetString(0) });
                }
                await tReader.CloseAsync();

                string colsQuery = @"
                    SELECT t.name AS TableName, c.name AS ColumnName, ty.name AS DataType
                    FROM sys.columns c
                    INNER JOIN sys.tables t ON c.object_id = t.object_id
                    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                    INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id
                    WHERE s.name = 'dbo'
                    ORDER BY t.name, c.column_id";

                using var cCmd = new SqlCommand(colsQuery, connection);
                using var cReader = await cCmd.ExecuteReaderAsync();
                while (await cReader.ReadAsync())
                {
                    DatabaseColumns.Add(new DatabaseColumnDto
                    {
                        TableName = cReader.GetString(0),
                        Name = cReader.GetString(1),
                        DataType = cReader.GetString(2)
                    });
                }
            }
            catch { }
        }

        private async Task<IActionResult> OnGetReloadAsync()
        {
            string? connStr = _configuration.GetConnectionString("DefaultConnection");
            if (connStr != null)
            {
                await LoadFormsListAsync(connStr);
                await LoadDatabaseSchemaOptionsAsync(connStr);
            }
            return Page();
        }

        public IActionResult OnGetPreview(long id)
        {
            return ViewComponent("DynamicForm", new { formId = id });
        }
    }
}
