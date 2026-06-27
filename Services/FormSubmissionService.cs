using System.Net.Mail;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using TalaPress.Models;
using TalaPress.Pages;

namespace TalaPress.Services;

public class FormDefinition
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Name_En { get; set; }
    public string? SuccessMessage { get; set; }
    public string? SuccessMessage_En { get; set; }
    public string ResponseStorageType { get; set; } = FormResponseStorageType.Database;
    public string? NotificationEmail { get; set; }
    public bool IsActive { get; set; }
    public List<FormFieldDto> Fields { get; set; } = new();
}

public class FormSubmissionResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public long? SubmissionId { get; set; }
    public int StatusCode { get; set; } = 200;
}

public interface IFormSubmissionService
{
    Task<FormDefinition?> GetActiveFormAsync(long formId);
    Task<FormSubmissionResult> SubmitAsync(
        long formId,
        Dictionary<string, JsonElement> submittedFields,
        string? ipAddress,
        string? userAgent,
        string? locale = null);
}

public class FormSubmissionService : IFormSubmissionService
{
    private const int MaxFieldLength = 4000;
    private const int MaxFields = 50;
    private static readonly Regex FieldNameRegex = new("^[A-Za-z][A-Za-z0-9_]*$", RegexOptions.Compiled);

    private readonly IConfiguration _configuration;
    private readonly ISmtpEmailService _smtpEmailService;

    public FormSubmissionService(IConfiguration configuration, ISmtpEmailService smtpEmailService)
    {
        _configuration = configuration;
        _smtpEmailService = smtpEmailService;
    }

    public async Task<FormDefinition?> GetActiveFormAsync(long formId)
    {
        string? connectionString = _configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString) || formId <= 0)
        {
            return null;
        }

        FormDefinition? form = null;
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        const string formQuery = @"
            SELECT Id, Name, Name_En, SuccessMessage, SuccessMessage_En,
                   ISNULL(ResponseStorageType, 'Database'), NotificationEmail, IsActive
            FROM dbo.Forms
            WHERE Id = @Id AND IsActive = 1";

        await using (var cmd = new SqlCommand(formQuery, connection))
        {
            cmd.Parameters.AddWithValue("@Id", formId);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return null;
            }

            form = new FormDefinition
            {
                Id = reader.GetInt64(0),
                Name = reader.GetString(1),
                Name_En = reader.IsDBNull(2) ? null : reader.GetString(2),
                SuccessMessage = reader.IsDBNull(3) ? null : reader.GetString(3),
                SuccessMessage_En = reader.IsDBNull(4) ? null : reader.GetString(4),
                ResponseStorageType = reader.IsDBNull(5) ? FormResponseStorageType.Database : reader.GetString(5),
                NotificationEmail = reader.IsDBNull(6) ? null : reader.GetString(6),
                IsActive = reader.GetBoolean(7)
            };
        }

        const string fieldsQuery = @"
            SELECT FieldName, Label, Label_En, FieldType, Placeholder, Placeholder_En,
                   HelpText, HelpText_En, IsRequired, DefaultValue, OptionsJson
            FROM dbo.FormFields
            WHERE FormId = @FormId AND IsActive = 1
            ORDER BY SortOrder";

        await using (var cmd = new SqlCommand(fieldsQuery, connection))
        {
            cmd.Parameters.AddWithValue("@FormId", formId);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                form.Fields.Add(new FormFieldDto
                {
                    FieldName = reader.GetString(0),
                    Label = reader.GetString(1),
                    Label_En = reader.IsDBNull(2) ? null : reader.GetString(2),
                    FieldType = reader.GetString(3),
                    Placeholder = reader.IsDBNull(4) ? null : reader.GetString(4),
                    Placeholder_En = reader.IsDBNull(5) ? null : reader.GetString(5),
                    HelpText = reader.IsDBNull(6) ? null : reader.GetString(6),
                    HelpText_En = reader.IsDBNull(7) ? null : reader.GetString(7),
                    IsRequired = reader.GetBoolean(8),
                    DefaultValue = reader.IsDBNull(9) ? null : reader.GetString(9),
                    OptionsJson = reader.IsDBNull(10) ? null : reader.GetString(10)
                });
            }
        }

        return form.Fields.Count == 0 ? null : form;
    }

    public async Task<FormSubmissionResult> SubmitAsync(
        long formId,
        Dictionary<string, JsonElement> submittedFields,
        string? ipAddress,
        string? userAgent,
        string? locale = null)
    {
        var form = await GetActiveFormAsync(formId);
        if (form == null)
        {
            return Fail("النموذج غير موجود أو معطل حالياً.", 404);
        }

        if (!FormResponseStorageType.IsValid(form.ResponseStorageType))
        {
            form.ResponseStorageType = FormResponseStorageType.Database;
        }

        var (normalized, validationError) = NormalizeAndValidate(form, submittedFields);
        if (validationError != null)
        {
            return Fail(validationError, 400);
        }

        bool storeDb = FormResponseStorageType.StoresInDatabase(form.ResponseStorageType);
        bool sendMail = FormResponseStorageType.SendsEmail(form.ResponseStorageType);

        if (sendMail)
        {
            string? recipient = form.NotificationEmail?.Trim();
            if (string.IsNullOrWhiteSpace(recipient))
            {
                recipient = await GetCompanyEmailAsync();
            }

            if (string.IsNullOrWhiteSpace(recipient))
            {
                return Fail("لم يُحدّد بريد استلام الإشعارات لهذا النموذج.", 400);
            }
        }

        long? submissionId = null;
        string submittedJson = JsonSerializer.Serialize(normalized);

        if (storeDb)
        {
            submissionId = await InsertSubmissionAsync(formId, submittedJson, ipAddress, userAgent);
            if (!submissionId.HasValue)
            {
                return Fail("فشل حفظ الرد في قاعدة البيانات.", 500);
            }
        }

        if (sendMail)
        {
            string recipient = form.NotificationEmail?.Trim() ?? await GetCompanyEmailAsync() ?? string.Empty;
            var (mailOk, mailError) = await SendNotificationEmailAsync(form, normalized, recipient, submissionId);
            if (!mailOk)
            {
                if (!storeDb)
                {
                    return Fail($"فشل إرسال البريد: {mailError}", 502);
                }
                // Stored successfully — report partial success in message.
                return new FormSubmissionResult
                {
                    Success = true,
                    SubmissionId = submissionId,
                    Message = GetSuccessMessage(form, locale) + " (تعذّر إرسال البريد.)"
                };
            }
        }

        return new FormSubmissionResult
        {
            Success = true,
            SubmissionId = submissionId,
            Message = GetSuccessMessage(form, locale)
        };
    }

    private static (Dictionary<string, string> Data, string? Error) NormalizeAndValidate(
        FormDefinition form,
        Dictionary<string, JsonElement> submittedFields)
    {
        if (submittedFields.Count > MaxFields)
        {
            return (new(), $"عدد الحقول يتجاوز الحد المسموح ({MaxFields}).");
        }

        var allowedNames = new HashSet<string>(
            form.Fields.Select(f => f.FieldName),
            StringComparer.OrdinalIgnoreCase);

        foreach (var key in submittedFields.Keys)
        {
            if (!FieldNameRegex.IsMatch(key) || !allowedNames.Contains(key))
            {
                return (new(), $"الحقل '{key}' غير مسموح.");
            }
        }

        var normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var field in form.Fields)
        {
            submittedFields.TryGetValue(field.FieldName, out JsonElement rawValue);
            string value = ExtractStringValue(rawValue);

            if (field.IsRequired && string.IsNullOrWhiteSpace(value))
            {
                return (new(), $"الحقل '{field.Label}' مطلوب.");
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                normalized[field.FieldName] = string.Empty;
                continue;
            }

            value = value.Trim();
            if (value.Length > MaxFieldLength)
            {
                return (new(), $"الحقل '{field.Label}' يتجاوز الحد الأقصى ({MaxFieldLength} حرف).");
            }

            string? typeError = ValidateFieldType(field, value);
            if (typeError != null)
            {
                return (new(), typeError);
            }

            normalized[field.FieldName] = value;
        }

        return (normalized, null);
    }

    private static string ExtractStringValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Array => string.Join(", ", element.EnumerateArray().Select(ExtractStringValue)),
            _ => string.Empty
        };
    }

    private static string? ValidateFieldType(FormFieldDto field, string value)
    {
        return field.FieldType switch
        {
            "Email" when !IsValidEmail(value) => $"قيمة '{field.Label}' ليست بريداً إلكترونياً صالحاً.",
            "Number" when !decimal.TryParse(value, out _) => $"قيمة '{field.Label}' يجب أن تكون رقماً.",
            "Phone" when value.Length > 30 => $"قيمة '{field.Label}' طويلة جداً.",
            _ => null
        };
    }

    private static bool IsValidEmail(string value)
    {
        try
        {
            _ = new MailAddress(value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<long?> InsertSubmissionAsync(long formId, string json, string? ip, string? userAgent)
    {
        string? connectionString = _configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        const string insertQuery = @"
            INSERT INTO dbo.FormSubmissions (FormId, SubmittedDataJson, IpAddress, UserAgent, CreatedAt)
            VALUES (@FormId, @Json, @Ip, @UserAgent, GETUTCDATE());
            SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";

        await using var cmd = new SqlCommand(insertQuery, connection);
        cmd.Parameters.AddWithValue("@FormId", formId);
        cmd.Parameters.AddWithValue("@Json", json);
        cmd.Parameters.AddWithValue("@Ip", (object?)ip ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@UserAgent", (object?)userAgent ?? DBNull.Value);

        object? result = await cmd.ExecuteScalarAsync();
        return result == null || result == DBNull.Value ? null : Convert.ToInt64(result);
    }

    private async Task<(bool Success, string? Error)> SendNotificationEmailAsync(
        FormDefinition form,
        Dictionary<string, string> data,
        string recipient,
        long? submissionId)
    {
        var sbHtml = new StringBuilder();
        sbHtml.Append("<h3>رد جديد على النموذج: ").Append(EscapeHtml(form.Name)).Append("</h3>");
        if (submissionId.HasValue)
        {
            sbHtml.Append("<p><strong>رقم الرد:</strong> ").Append(submissionId.Value).Append("</p>");
        }
        sbHtml.Append("<table border=\"1\" cellpadding=\"8\" cellspacing=\"0\" style=\"border-collapse:collapse;\">");
        foreach (var field in form.Fields)
        {
            data.TryGetValue(field.FieldName, out string value);
            sbHtml.Append("<tr><th>").Append(EscapeHtml(field.Label)).Append("</th><td>")
                .Append(EscapeHtml(value)).Append("</td></tr>");
        }
        sbHtml.Append("</table>");

        var sbText = new StringBuilder();
        sbText.AppendLine($"رد جديد: {form.Name}");
        foreach (var field in form.Fields)
        {
            data.TryGetValue(field.FieldName, out string value);
            sbText.AppendLine($"{field.Label}: {value}");
        }

        return await _smtpEmailService.SendAsync(
            recipient,
            $"[TalaPress] رد جديد — {form.Name}",
            sbHtml.ToString(),
            sbText.ToString());
    }

    private async Task<string?> GetCompanyEmailAsync()
    {
        string? connectionString = _configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            await using var cmd = new SqlCommand("SELECT CompanyEmail FROM dbo.Settings WHERE Id = 1", connection);
            object? result = await cmd.ExecuteScalarAsync();
            return result == null || result == DBNull.Value ? null : result.ToString();
        }
        catch
        {
            return null;
        }
    }

    private static string GetSuccessMessage(FormDefinition form, string? locale)
    {
        bool english = locale?.StartsWith("en", StringComparison.OrdinalIgnoreCase) == true;
        if (english && !string.IsNullOrWhiteSpace(form.SuccessMessage_En))
        {
            return form.SuccessMessage_En;
        }
        return form.SuccessMessage ?? "تم إرسال رسالتك بنجاح.";
    }

    private static string EscapeHtml(string text) =>
        WebUtility.HtmlEncode(text);

    private static FormSubmissionResult Fail(string message, int statusCode) =>
        new() { Success = false, Message = message, StatusCode = statusCode };
}
