using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using TalaPress.Api.Security;

namespace TalaPress.Api.Controllers
{
    [ApiController]
    [Route("api/v1/settings")]
    [Authorize(AuthenticationSchemes = PearlAuthenticationDefaults.AuthenticationScheme)]
    public class SettingsController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public SettingsController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet]
        public async Task<IActionResult> GetSettings()
        {
            string? connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return Problem("Database connection is not configured.");
            }

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            const string selectQuery = @"
                SELECT SiteName, SiteName_En, SiteDescription, SiteDescription_En, Logo, Favicon,
                       DefaultLanguage, SupportedLanguages, Theme, AccentColor, EnableDarkMode, EnableRTL,
                       DateFormat, TimeFormat, DateTimeFormat, TimeZone, DefaultPageSize, MaxUploadSizeMB,
                       AllowedFileExtensions, EnableSeo, EnableCategories, EnableMediaLibrary, EnableContentVersioning,
                       AutoGenerateSlug, DefaultContentStatus, ApiEnabled, GoogleAnalyticsCode, GoogleTagManagerCode,
                       CompanyName, CompanyName_En, CompanyEmail, CompanyPhone, CompanyAddress, CompanyAddress_En,
                       FacebookUrl, TwitterUrl, InstagramUrl, LinkedInUrl, YouTubeUrl, FooterCopyright, FooterCopyright_En,
                       ShowHits
                FROM dbo.Settings
                WHERE Id = 1";

            await using var command = new SqlCommand(selectQuery, connection);
            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var settings = new
                {
                    siteName = reader.GetString(0),
                    siteNameEn = reader.IsDBNull(1) ? null : reader.GetString(1),
                    siteDescription = reader.IsDBNull(2) ? null : reader.GetString(2),
                    siteDescriptionEn = reader.IsDBNull(3) ? null : reader.GetString(3),
                    logo = reader.IsDBNull(4) ? null : reader.GetString(4),
                    favicon = reader.IsDBNull(5) ? null : reader.GetString(5),
                    defaultLanguage = reader.GetString(6),
                    supportedLanguages = reader.GetString(7),
                    theme = reader.GetString(8),
                    accentColor = reader.IsDBNull(9) ? null : reader.GetString(9),
                    enableDarkMode = reader.GetBoolean(10),
                    enableRTL = reader.GetBoolean(11),
                    dateFormat = reader.GetString(12),
                    timeFormat = reader.GetString(13),
                    dateTimeFormat = reader.GetString(14),
                    timeZone = reader.GetString(15),
                    defaultPageSize = reader.GetInt32(16),
                    maxUploadSizeMB = reader.GetInt32(17),
                    allowedFileExtensions = reader.IsDBNull(18) ? null : reader.GetString(18),
                    enableSeo = reader.GetBoolean(19),
                    enableCategories = reader.GetBoolean(20),
                    enableMediaLibrary = reader.GetBoolean(21),
                    enableContentVersioning = reader.GetBoolean(22),
                    autoGenerateSlug = reader.GetBoolean(23),
                    defaultContentStatus = reader.GetString(24),
                    apiEnabled = reader.GetBoolean(25),
                    googleAnalyticsCode = reader.IsDBNull(26) ? null : reader.GetString(26),
                    googleTagManagerCode = reader.IsDBNull(27) ? null : reader.GetString(27),
                    companyName = reader.IsDBNull(28) ? null : reader.GetString(28),
                    companyNameEn = reader.IsDBNull(29) ? null : reader.GetString(29),
                    companyEmail = reader.IsDBNull(30) ? null : reader.GetString(30),
                    companyPhone = reader.IsDBNull(31) ? null : reader.GetString(31),
                    companyAddress = reader.IsDBNull(32) ? null : reader.GetString(32),
                    companyAddressEn = reader.IsDBNull(33) ? null : reader.GetString(33),
                    facebookUrl = reader.IsDBNull(34) ? null : reader.GetString(34),
                    twitterUrl = reader.IsDBNull(35) ? null : reader.GetString(35),
                    instagramUrl = reader.IsDBNull(36) ? null : reader.GetString(36),
                    linkedInUrl = reader.IsDBNull(37) ? null : reader.GetString(37),
                    youTubeUrl = reader.IsDBNull(38) ? null : reader.GetString(38),
                    footerCopyright = reader.IsDBNull(39) ? null : reader.GetString(39),
                    footerCopyrightEn = reader.IsDBNull(40) ? null : reader.GetString(40),
                    showHits = reader.IsDBNull(41) ? true : reader.GetBoolean(41)
                };

                return Ok(settings);
            }

            return NotFound(new { message = "Settings not found." });
        }
    }
}
