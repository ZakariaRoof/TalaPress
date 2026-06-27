using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using System.Text.Json;
using TalaPress.Infrastructure;

namespace TalaPress.Pages
{
    public class FormSubmissionDto
    {
        public long Id { get; set; }
        public long FormId { get; set; }
        public string SubmittedDataJson { get; set; } = string.Empty;
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
        public DateTime CreatedAt { get; set; }
        
        // Helper to get formatted dictionary
        public Dictionary<string, string> Data => 
            string.IsNullOrEmpty(SubmittedDataJson) 
            ? new Dictionary<string, string>() 
            : JsonSerializer.Deserialize<Dictionary<string, string>>(SubmittedDataJson) ?? new Dictionary<string, string>();
    }

    public class FormSubmissionsModel : PageModel
    {
        private readonly IConfiguration _configuration;

        public FormSubmissionsModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public List<FormViewModel> FormsList { get; set; } = new();
        public List<FormSubmissionDto> SubmissionsList { get; set; } = new();
        
        [BindProperty(SupportsGet = true)]
        public long? SelectedFormId { get; set; }

        public string? SelectedFormName { get; set; }
        public List<FormFieldDto> FormFields { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            if (User.Identity?.IsAuthenticated != true) return RedirectToPage("/Login");
            if (!User.HasClaim("Permission", "FormSubmissions.View"))
            {
                TempData["ErrorMessage"] = "ليس لديك صلاحية عرض الردود.";
                return RedirectToPage("/Index");
            }

            string? connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString)) return Page();

            await FormsSchemaHelper.EnsureAsync(connectionString);
            await LoadFormsAsync(connectionString);

            if (SelectedFormId.HasValue && SelectedFormId.Value > 0)
            {
                var form = FormsList.FirstOrDefault(f => f.Id == SelectedFormId.Value);
                if (form != null)
                {
                    SelectedFormName = form.Name;
                    await LoadFormFieldsAsync(connectionString, SelectedFormId.Value);
                    await LoadSubmissionsAsync(connectionString, SelectedFormId.Value);
                }
            }

            return Page();
        }

        private async Task LoadFormsAsync(string connectionString)
        {
            FormsList.Clear();
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            using var command = new SqlCommand("SELECT Id, Name FROM dbo.Forms ORDER BY CreatedAt DESC", connection);
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                FormsList.Add(new FormViewModel
                {
                    Id = reader.GetInt64(0),
                    Name = reader.GetString(1)
                });
            }
        }

        private async Task LoadFormFieldsAsync(string connectionString, long formId)
        {
            FormFields.Clear();
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            using var command = new SqlCommand("SELECT FieldName, Label, Label_En, FieldType FROM dbo.FormFields WHERE FormId=@FormId ORDER BY SortOrder", connection);
            command.Parameters.AddWithValue("@FormId", formId);
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                FormFields.Add(new FormFieldDto
                {
                    FieldName = reader.GetString(0),
                    Label = reader.GetString(1),
                    Label_En = reader.IsDBNull(2) ? null : reader.GetString(2),
                    FieldType = reader.GetString(3)
                });
            }
        }

        private async Task LoadSubmissionsAsync(string connectionString, long formId)
        {
            SubmissionsList.Clear();
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            using var command = new SqlCommand("SELECT Id, FormId, SubmittedDataJson, IpAddress, UserAgent, CreatedAt FROM dbo.FormSubmissions WHERE FormId=@FormId ORDER BY CreatedAt DESC", connection);
            command.Parameters.AddWithValue("@FormId", formId);
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                SubmissionsList.Add(new FormSubmissionDto
                {
                    Id = reader.GetInt64(0),
                    FormId = reader.GetInt64(1),
                    SubmittedDataJson = reader.GetString(2),
                    IpAddress = reader.IsDBNull(3) ? null : reader.GetString(3),
                    UserAgent = reader.IsDBNull(4) ? null : reader.GetString(4),
                    CreatedAt = reader.GetDateTime(5)
                });
            }
        }
    }
}
