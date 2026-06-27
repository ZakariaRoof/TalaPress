using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Text.Json;
using TalaPress.Api.Security;
using TalaPress.Services;

namespace TalaPress.Api.Controllers;

[ApiController]
[Route("api/v1/forms")]
public class FormsController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly IFormSubmissionService _formSubmissionService;

    public FormsController(IConfiguration configuration, IFormSubmissionService formSubmissionService)
    {
        _configuration = configuration;
        _formSubmissionService = formSubmissionService;
    }

    [HttpGet]
    [Authorize(AuthenticationSchemes = PearlAuthenticationDefaults.AuthenticationScheme)]
    public async Task<IActionResult> GetAll([FromQuery] bool activeOnly = true)
    {
        string? connectionString = _configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return Problem("Database connection is not configured.");
        }

        var items = new List<object>();
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        string query = activeOnly
            ? @"SELECT Id, Name, Name_En, Description, Description_En, SubmitButtonText, SubmitButtonText_En,
                       SuccessMessage, SuccessMessage_En, ISNULL(ResponseStorageType, 'Database') AS ResponseStorageType,
                       IsActive, CreatedAt
                FROM dbo.Forms WHERE IsActive = 1 ORDER BY Name"
            : @"SELECT Id, Name, Name_En, Description, Description_En, SubmitButtonText, SubmitButtonText_En,
                       SuccessMessage, SuccessMessage_En, ISNULL(ResponseStorageType, 'Database') AS ResponseStorageType,
                       IsActive, CreatedAt
                FROM dbo.Forms ORDER BY Name";

        await using var command = new SqlCommand(query, connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new
            {
                id = reader.GetInt64(0),
                name = reader.GetString(1),
                nameEn = reader.IsDBNull(2) ? null : reader.GetString(2),
                description = reader.IsDBNull(3) ? null : reader.GetString(3),
                descriptionEn = reader.IsDBNull(4) ? null : reader.GetString(4),
                submitButtonText = reader.IsDBNull(5) ? null : reader.GetString(5),
                submitButtonTextEn = reader.IsDBNull(6) ? null : reader.GetString(6),
                successMessage = reader.IsDBNull(7) ? null : reader.GetString(7),
                successMessageEn = reader.IsDBNull(8) ? null : reader.GetString(8),
                responseStorageType = reader.GetString(9),
                isActive = reader.GetBoolean(10),
                createdAt = reader.GetDateTime(11)
            });
        }

        return Ok(new { items });
    }

    [HttpGet("{id:long}")]
    [Authorize(AuthenticationSchemes = PearlAuthenticationDefaults.AuthenticationScheme)]
    public async Task<IActionResult> GetById(long id)
    {
        var form = await _formSubmissionService.GetActiveFormAsync(id);
        if (form == null)
        {
            return NotFound(new { message = "Form not found or inactive." });
        }

        return Ok(new
        {
            id = form.Id,
            name = form.Name,
            nameEn = form.Name_En,
            successMessage = form.SuccessMessage,
            successMessageEn = form.SuccessMessage_En,
            responseStorageType = form.ResponseStorageType,
            fields = form.Fields.Select(f => new
            {
                fieldName = f.FieldName,
                label = f.Label,
                labelEn = f.Label_En,
                fieldType = f.FieldType,
                placeholder = f.Placeholder,
                placeholderEn = f.Placeholder_En,
                helpText = f.HelpText,
                helpTextEn = f.HelpText_En,
                isRequired = f.IsRequired,
                defaultValue = f.DefaultValue,
                optionsJson = f.OptionsJson
            })
        });
    }

    [HttpPost("{id:long}/submit")]
    [Authorize(AuthenticationSchemes = PearlAuthenticationDefaults.AuthenticationScheme)]
    public async Task<IActionResult> Submit(long id, [FromBody] JsonElement body)
    {
        var fields = ExtractFields(body, out string? locale);
        if (fields.Count == 0)
        {
            return BadRequest(new { success = false, message = "No field data submitted." });
        }

        string ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        string userAgent = Request.Headers.UserAgent.ToString();

        var result = await _formSubmissionService.SubmitAsync(id, fields, ip, userAgent, locale);

        if (!result.Success)
        {
            return StatusCode(result.StatusCode, new { success = false, message = result.Message });
        }

        return Ok(new
        {
            success = true,
            message = result.Message,
            submissionId = result.SubmissionId
        });
    }

    private static Dictionary<string, JsonElement> ExtractFields(JsonElement body, out string? locale)
    {
        locale = null;
        var fields = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

        if (body.ValueKind != JsonValueKind.Object)
        {
            return fields;
        }

        if (body.TryGetProperty("locale", out var localeEl) && localeEl.ValueKind == JsonValueKind.String)
        {
            locale = localeEl.GetString();
        }

        JsonElement dataRoot = body;
        if (body.TryGetProperty("fields", out var fieldsEl) && fieldsEl.ValueKind == JsonValueKind.Object)
        {
            dataRoot = fieldsEl;
        }

        foreach (var prop in dataRoot.EnumerateObject())
        {
            if (prop.NameEquals("locale") || prop.NameEquals("formId"))
            {
                continue;
            }
            fields[prop.Name] = prop.Value;
        }

        return fields;
    }
}
