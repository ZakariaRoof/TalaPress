using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using TalaPress.Api.Security;

namespace TalaPress.Api.Controllers;

[ApiController]
[Route("api/v1/project-types")]
[Authorize(AuthenticationSchemes = PearlAuthenticationDefaults.AuthenticationScheme)]
public sealed class ProjectTypesController : ControllerBase
{
    private const string QatarCharityExportPathBaseUrl = "https://portal.qcharity.net/ExportPath/";

    private readonly IConfiguration _configuration;
    private readonly ILogger<ProjectTypesController> _logger;

    public ProjectTypesController(IConfiguration configuration, ILogger<ProjectTypesController> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetProjectTypes(
        [FromQuery] int languageId = 1,
        CancellationToken cancellationToken = default)
    {
        var connectionString = _configuration.GetConnectionString("SponsorshipsConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return Problem("QC project types data source is not configured.");
        }

        var normalizedLanguageId = languageId == 2 ? 2 : 1;

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            var items = await LoadProjectTypesAsync(connection, normalizedLanguageId, cancellationToken);
            return Ok(new
            {
                items,
                data = items,
                meta = new
                {
                    languageId = normalizedLanguageId,
                    totalCount = items.Count
                }
            });
        }
        catch (SqlException ex)
        {
            _logger.LogError(ex, "Failed to fetch QC project types.");
            return Problem("Failed to fetch QC project types.");
        }
    }

    private static async Task<List<ProjectTypeDto>> LoadProjectTypesAsync(
        SqlConnection connection,
        int languageId,
        CancellationToken cancellationToken)
    {
        const string query = @"
DECLARE @LangId INT = @LanguageId;

SELECT
    0 AS ProjectTypeId,
    CASE
        WHEN @LangId = 1 THEN N'أنواع المشاريع'
        ELSE N'Project Types'
    END AS ProjectTypeName,
    0 AS IsHaveContribution,
    NULL AS ActivityAccountId,
    N'' AS TitleAr,
    N'' AS DescriptionAr,
    N'' AS SmallImage,
    N'' AS LargeImage

UNION ALL

SELECT
    TypeId AS ProjectTypeId,
    CASE
        WHEN @LangId = 1 THEN ArName
        ELSE ISNULL(NULLIF(EnName, ''), ArName)
    END AS ProjectTypeName,
    IsHaveContribution,
    ActivityAccountId,
    TitleAr,
    DescriptionAr,
    SmallImage,
    LargeImage
FROM qc.dbo.ProjectTypes
WHERE ISNULL(IsCore, 0) = 0
  AND IsActiveProjectType = 1
  AND SmallImage IS NOT NULL
  AND LTRIM(RTRIM(SmallImage)) <> ''

ORDER BY ProjectTypeId;";

        var items = new List<ProjectTypeDto>();
        await using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@LanguageId", languageId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var projectTypeName = ReadString(reader, "ProjectTypeName") ?? string.Empty;
            var titleAr = ReadString(reader, "TitleAr") ?? string.Empty;
            var descriptionAr = ReadString(reader, "DescriptionAr") ?? string.Empty;
            var title = BuildCardTitle(titleAr, descriptionAr, projectTypeName);

            items.Add(new ProjectTypeDto
            {
                ProjectTypeId = ReadInt(reader, "ProjectTypeId") ?? 0,
                ProjectTypeName = projectTypeName,
                IsHaveContribution = ReadInt(reader, "IsHaveContribution") ?? 0,
                ActivityAccountId = ReadInt(reader, "ActivityAccountId"),
                TitleAr = titleAr,
                DescriptionAr = descriptionAr,
                SmallImage = BuildQatarCharityImageUrl(ReadString(reader, "SmallImage")),
                LargeImage = BuildQatarCharityImageUrl(ReadString(reader, "LargeImage")),
                Title = title,
                Description = descriptionAr.Trim()
            });
        }

        return items;
    }

    private static string BuildCardTitle(string? titleAr, string? descriptionAr, string projectTypeName)
    {
        if (!string.IsNullOrWhiteSpace(titleAr))
        {
            return titleAr.Trim();
        }

        if (!string.IsNullOrWhiteSpace(descriptionAr))
        {
            var fallback = descriptionAr.Trim();
            return fallback.Length <= 150 ? fallback : fallback[..150].TrimEnd();
        }

        return projectTypeName.Trim();
    }

    private static string BuildQatarCharityImageUrl(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var trimmed = path.Trim();
        if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        return QatarCharityExportPathBaseUrl + trimmed.TrimStart('/');
    }

    private static string? ReadString(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : Convert.ToString(reader.GetValue(ordinal))?.Trim();
    }

    private static int? ReadInt(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : Convert.ToInt32(reader.GetValue(ordinal));
    }

    private sealed class ProjectTypeDto
    {
        public int ProjectTypeId { get; set; }
        public string ProjectTypeName { get; set; } = string.Empty;
        public int IsHaveContribution { get; set; }
        public int? ActivityAccountId { get; set; }
        public string TitleAr { get; set; } = string.Empty;
        public string DescriptionAr { get; set; } = string.Empty;
        public string SmallImage { get; set; } = string.Empty;
        public string LargeImage { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}