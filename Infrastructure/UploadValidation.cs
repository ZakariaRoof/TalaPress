using Microsoft.Data.SqlClient;

namespace TalaPress.Infrastructure;

public static class UploadValidation
{
    private static readonly string[] BlockedExtensions =
    {
        "exe", "dll", "bat", "cmd", "sh", "php", "asp", "aspx", "cshtml", "jsp", "py", "pl", "rb", "js", "config", "json", "html", "htm", "svg", "msi"
    };

    public static async Task<(int MaxUploadSizeMb, string AllowedFileExtensions)> LoadSettingsAsync(string connectionString)
    {
        const int defaultMaxMb = 20;
        const string defaultAllowed = "jpg,jpeg,png,gif,webp,pdf,doc,docx,xls,xlsx,ppt,pptx,zip,mp4,mp3";

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            const string query = "SELECT MaxUploadSizeMB, AllowedFileExtensions FROM dbo.Settings WHERE Id = 1";
            await using var command = new SqlCommand(query, connection);
            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                int maxMb = reader.IsDBNull(0) ? defaultMaxMb : reader.GetInt32(0);
                string allowed = reader.IsDBNull(1) ? defaultAllowed : reader.GetString(1);
                return (Math.Max(1, maxMb), allowed);
            }
        }
        catch (SqlException)
        {
            // Settings table may not exist yet during first run.
        }

        return (defaultMaxMb, defaultAllowed);
    }

    public static string? ValidateFile(IFormFile file, int maxUploadSizeMb, string allowedFileExtensions)
    {
        if (file.Length == 0)
        {
            return "لم يتم اختيار ملف.";
        }

        string ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (string.IsNullOrEmpty(ext))
        {
            return "نوع الملف غير صالح (لا يحتوي على امتداد).";
        }

        var allowedExts = allowedFileExtensions
            .Split(new[] { ',', '|', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(e => e.Trim().ToLowerInvariant().TrimStart('.'))
            .ToList();

        string extWithoutDot = ext.TrimStart('.');
        if (BlockedExtensions.Contains(extWithoutDot) || !allowedExts.Contains(extWithoutDot))
        {
            return $"نوع الملف غير مسموح. الامتدادات المدعومة: {allowedFileExtensions}";
        }

        long maxBytes = (long)maxUploadSizeMb * 1024 * 1024;
        if (file.Length > maxBytes)
        {
            return $"حجم الملف يتجاوز الحد الأقصى ({maxUploadSizeMb} MB).";
        }

        return null;
    }
}
