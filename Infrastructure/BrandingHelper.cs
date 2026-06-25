using Microsoft.Data.SqlClient;

namespace TalaPress.Infrastructure;

public static class BrandingHelper
{
    public static readonly string[] LogoExtensions = { ".png", ".jpg", ".jpeg", ".gif", ".webp", ".svg" };
    public static readonly string[] FaviconExtensions = { ".ico", ".png", ".jpg", ".jpeg", ".gif", ".webp" };

    public static string GetBrandingDirectory(string webRootPath)
        => Path.Combine(webRootPath, "uploads", "img");

    public static (string? LogoPath, string? FaviconPath) ResolveFromDisk(string webRootPath)
    {
        var imgDir = GetBrandingDirectory(webRootPath);
        if (!Directory.Exists(imgDir))
        {
            return (null, null);
        }

        string? logo = FindFirstFile(imgDir, "CMSLogo.*");
        string? favicon = FindFirstFile(imgDir, "CMSFavicon.*");
        return (logo, favicon);
    }

    public static async Task<(string? LogoPath, string? FaviconPath)> ResolveAsync(
        string connectionString,
        string webRootPath)
    {
        var (diskLogo, diskFavicon) = ResolveFromDisk(webRootPath);
        string? dbLogo = null;
        string? dbFavicon = null;

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            const string query = "SELECT Logo, Favicon FROM dbo.Settings WHERE Id = 1";
            await using var command = new SqlCommand(query, connection);
            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                dbLogo = reader.IsDBNull(0) ? null : reader.GetString(0);
                dbFavicon = reader.IsDBNull(1) ? null : reader.GetString(1);
            }
        }
        catch (SqlException)
        {
            // Fall back to disk-only resolution.
        }

        return (
            PickExistingPath(webRootPath, dbLogo, diskLogo),
            PickExistingPath(webRootPath, dbFavicon, diskFavicon));
    }

    public static async Task<(string? WebPath, string? Error)> SaveBrandingFileAsync(
        IFormFile file,
        string webRootPath,
        string filePrefix,
        string[] allowedExtensions,
        int maxUploadSizeMb)
    {
        if (file.Length == 0)
        {
            return (null, "لم يتم اختيار ملف.");
        }

        string ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (string.IsNullOrEmpty(ext) || !allowedExtensions.Contains(ext))
        {
            return (null, $"نوع الملف غير مدعوم. الامتدادات المسموحة: {string.Join(", ", allowedExtensions)}");
        }

        long maxBytes = (long)maxUploadSizeMb * 1024 * 1024;
        if (file.Length > maxBytes)
        {
            return (null, $"حجم الملف يتجاوز الحد الأقصى ({maxUploadSizeMb} MB).");
        }

        var imgDir = GetBrandingDirectory(webRootPath);
        try
        {
            Directory.CreateDirectory(imgDir);
        }
        catch (Exception ex)
        {
            return (null, $"تعذّر إنشاء مجلد الرفع: {ex.Message}");
        }

        foreach (var oldFile in Directory.GetFiles(imgDir, filePrefix + ".*"))
        {
            try { File.Delete(oldFile); } catch { /* best effort */ }
        }

        string fileName = filePrefix + ext;
        string destPath = Path.Combine(imgDir, fileName);

        try
        {
            await using var stream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await file.CopyToAsync(stream);
        }
        catch (UnauthorizedAccessException)
        {
            return (null, "لا توجد صلاحية كتابة على مجلد uploads/img. راجع صلاحيات IIS/Plesk لمجلد wwwroot/uploads.");
        }
        catch (Exception ex)
        {
            return (null, $"تعذّر حفظ الملف: {ex.Message}");
        }

        return ("/uploads/img/" + fileName, null);
    }

    private static string? PickExistingPath(string webRootPath, string? dbPath, string? diskPath)
    {
        if (TryMapWebPath(webRootPath, dbPath, out var resolvedDbPath))
        {
            return resolvedDbPath;
        }

        return diskPath;
    }

    private static bool TryMapWebPath(string webRootPath, string? webPath, out string? resolvedPath)
    {
        resolvedPath = null;
        if (string.IsNullOrWhiteSpace(webPath) || !webPath.StartsWith('/'))
        {
            return false;
        }

        var relativePath = webPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var physicalPath = Path.Combine(webRootPath, relativePath);
        if (!File.Exists(physicalPath))
        {
            return false;
        }

        resolvedPath = webPath;
        return true;
    }

    private static string? FindFirstFile(string directory, string pattern)
    {
        var files = Directory.GetFiles(directory, pattern);
        if (files.Length == 0)
        {
            return null;
        }

        return "/uploads/img/" + Path.GetFileName(files[0]);
    }
}
