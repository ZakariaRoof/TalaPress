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
                SELECT SiteName, SiteName_En, SiteDescription, SiteDescription_En, SiteUrl, Logo, LogoLight, Favicon,
                       DefaultLanguage, SupportedLanguages, Theme, AccentColor, EnableDarkMode, EnableRTL,
                       DateFormat, TimeFormat, DateTimeFormat, TimeZone, DefaultPageSize, MaxUploadSizeMB,
                       AllowedFileExtensions, EnableSeo, EnableCategories, EnableMediaLibrary, EnableContentVersioning,
                       AutoGenerateSlug, DefaultContentStatus, ApiEnabled, GoogleAnalyticsCode, GoogleTagManagerCode,
                       CompanyName, CompanyName_En, CompanyEmail, CompanyPhone, CompanyAddress, CompanyAddress_En, CompanyMapUrl,
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
                    siteUrl = reader.IsDBNull(4) ? null : reader.GetString(4),
                    logo = reader.IsDBNull(5) ? null : reader.GetString(5),
                    logoLight = reader.IsDBNull(6) ? null : reader.GetString(6),
                    favicon = reader.IsDBNull(7) ? null : reader.GetString(7),
                    defaultLanguage = reader.GetString(8),
                    supportedLanguages = reader.GetString(9),
                    theme = reader.GetString(10),
                    accentColor = reader.IsDBNull(11) ? null : reader.GetString(11),
                    enableDarkMode = reader.GetBoolean(12),
                    enableRTL = reader.GetBoolean(13),
                    dateFormat = reader.GetString(14),
                    timeFormat = reader.GetString(15),
                    dateTimeFormat = reader.GetString(16),
                    timeZone = reader.GetString(17),
                    defaultPageSize = reader.GetInt32(18),
                    maxUploadSizeMB = reader.GetInt32(19),
                    allowedFileExtensions = reader.IsDBNull(20) ? null : reader.GetString(20),
                    enableSeo = reader.GetBoolean(21),
                    enableCategories = reader.GetBoolean(22),
                    enableMediaLibrary = reader.GetBoolean(23),
                    enableContentVersioning = reader.GetBoolean(24),
                    autoGenerateSlug = reader.GetBoolean(25),
                    defaultContentStatus = reader.GetString(26),
                    apiEnabled = reader.GetBoolean(27),
                    googleAnalyticsCode = reader.IsDBNull(28) ? null : reader.GetString(28),
                    googleTagManagerCode = reader.IsDBNull(29) ? null : reader.GetString(29),
                    companyName = reader.IsDBNull(30) ? null : reader.GetString(30),
                    companyNameEn = reader.IsDBNull(31) ? null : reader.GetString(31),
                    companyEmail = reader.IsDBNull(32) ? null : reader.GetString(32),
                    companyPhone = reader.IsDBNull(33) ? null : reader.GetString(33),
                    companyAddress = reader.IsDBNull(34) ? null : reader.GetString(34),
                    companyAddressEn = reader.IsDBNull(35) ? null : reader.GetString(35),
                    companyMapUrl = reader.IsDBNull(36) ? null : reader.GetString(36),
                    facebookUrl = reader.IsDBNull(37) ? null : reader.GetString(37),
                    twitterUrl = reader.IsDBNull(38) ? null : reader.GetString(38),
                    instagramUrl = reader.IsDBNull(39) ? null : reader.GetString(39),
                    linkedInUrl = reader.IsDBNull(40) ? null : reader.GetString(40),
                    youTubeUrl = reader.IsDBNull(41) ? null : reader.GetString(41),
                    footerCopyright = reader.IsDBNull(42) ? null : reader.GetString(42),
                    footerCopyrightEn = reader.IsDBNull(43) ? null : reader.GetString(43),
                    showHits = reader.IsDBNull(44) ? true : reader.GetBoolean(44)
                };

                return Ok(settings);
            }

            return NotFound(new { message = "Settings not found." });
        }
    }
}
