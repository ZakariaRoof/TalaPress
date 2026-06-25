using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace TalaPress.Controllers
{
    [Route("api/forms")]
    [ApiController]
    public class FormController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public FormController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public class FormSubmitRequest
        {
            public long FormId { get; set; }
            public string SubmittedDataJson { get; set; } = string.Empty;
        }

        [HttpPost("submit")]
        [IgnoreAntiforgeryToken] // Should implement custom header check for Antiforgery in a real production env
        public async Task<IActionResult> SubmitForm([FromBody] FormSubmitRequest request)
        {
            if (request.FormId <= 0 || string.IsNullOrWhiteSpace(request.SubmittedDataJson))
            {
                return BadRequest(new { success = false, message = "بيانات غير صالحة." });
            }

            string? connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                return StatusCode(500, new { success = false, message = "خطأ في الاتصال بقاعدة البيانات." });
            }

            try
            {
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                // 1. Verify Form Exists & Active
                string successMessage = "تم إرسال رسالتك بنجاح.";
                using (var cmd = new SqlCommand("SELECT SuccessMessage, SuccessMessage_En FROM dbo.Forms WHERE Id = @Id AND IsActive = 1", connection))
                {
                    cmd.Parameters.AddWithValue("@Id", request.FormId);
                    using var reader = await cmd.ExecuteReaderAsync();
                    if (!await reader.ReadAsync())
                    {
                        return BadRequest(new { success = false, message = "النموذج غير موجود أو معطل حالياً." });
                    }
                    
                    if (!reader.IsDBNull(0)) successMessage = reader.GetString(0);
                    // English success message logic can be applied based on UI culture if needed.
                }

                string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
                string userAgent = Request.Headers["User-Agent"].ToString() ?? "Unknown";

                // 2. Insert Submission
                string insertQuery = @"
                    INSERT INTO dbo.FormSubmissions (FormId, SubmittedDataJson, IpAddress, UserAgent, CreatedAt)
                    VALUES (@FormId, @SubmittedDataJson, @IpAddress, @UserAgent, GETUTCDATE())";

                using (var cmd = new SqlCommand(insertQuery, connection))
                {
                    cmd.Parameters.AddWithValue("@FormId", request.FormId);
                    cmd.Parameters.AddWithValue("@SubmittedDataJson", request.SubmittedDataJson);
                    cmd.Parameters.AddWithValue("@IpAddress", ipAddress);
                    cmd.Parameters.AddWithValue("@UserAgent", userAgent);

                    await cmd.ExecuteNonQueryAsync();
                }

                // 3. Email Notification (Placeholder, depending on SendEmailNotification config)
                // If the user checked "SendEmailNotification", we could use an ISmtpService here.

                return Ok(new { success = true, message = successMessage });
            }
            catch (Exception ex)
            {
                // Log exception
                return StatusCode(500, new { success = false, message = "حدث خطأ غير متوقع أثناء معالجة الطلب." });
            }
        }
    }
}
