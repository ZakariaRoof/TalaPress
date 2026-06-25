using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using TalaPress.Infrastructure;

namespace TalaPress.Pages
{
    public class SettingsModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;

        public SettingsModel(IConfiguration configuration, IWebHostEnvironment environment)
        {
            _configuration = configuration;
            _environment = environment;
        }

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        // Bindable properties matching the Settings database schema
        [BindProperty]
        public string SiteName { get; set; } = string.Empty;

        [BindProperty]
        public string? SiteName_En { get; set; }

        [BindProperty]
        public string? SiteDescription { get; set; }

        [BindProperty]
        public string? SiteDescription_En { get; set; }

        [BindProperty]
        public string? Logo { get; set; }

        [BindProperty]
        public IFormFile? LogoFile { get; set; }

        [BindProperty]
        public string? Favicon { get; set; }

        [BindProperty]
        public IFormFile? FaviconFile { get; set; }

        [BindProperty]
        public string DefaultLanguage { get; set; } = "ar";

        [BindProperty]
        public string SupportedLanguages { get; set; } = "ar,en";

        [BindProperty]
        public string Theme { get; set; } = "Light";

        [BindProperty]
        public string? AccentColor { get; set; }

        [BindProperty]
        public bool EnableDarkMode { get; set; }

        [BindProperty]
        public bool EnableRTL { get; set; }

        [BindProperty]
        public string DateFormat { get; set; } = "dd/MM/yyyy";

        [BindProperty]
        public string TimeFormat { get; set; } = "HH:mm";

        [BindProperty]
        public string DateTimeFormat { get; set; } = "dd/MM/yyyy HH:mm";

        [BindProperty]
        public string TimeZone { get; set; } = "Arab Standard Time";

        [BindProperty]
        public int DefaultPageSize { get; set; } = 10;

        [BindProperty]
        public int MaxUploadSizeMB { get; set; } = 20;

        [BindProperty]
        public string? AllowedFileExtensions { get; set; }

        [BindProperty]
        public bool EnableSeo { get; set; }

        [BindProperty]
        public bool EnableCategories { get; set; }

        [BindProperty]
        public bool EnableMediaLibrary { get; set; }

        [BindProperty]
        public bool EnableContentVersioning { get; set; }

        [BindProperty]
        public bool AutoGenerateSlug { get; set; }

        [BindProperty]
        public string DefaultContentStatus { get; set; } = "Draft";

        [BindProperty]
        public bool ApiEnabled { get; set; }

        [BindProperty]
        public string? GoogleAnalyticsCode { get; set; }

        [BindProperty]
        public string? GoogleTagManagerCode { get; set; }

        [BindProperty]
        public string? CompanyName { get; set; }

        [BindProperty]
        public string? CompanyName_En { get; set; }

        [BindProperty]
        public string? CompanyEmail { get; set; }

        [BindProperty]
        public string? CompanyPhone { get; set; }

        [BindProperty]
        public string? CompanyAddress { get; set; }

        [BindProperty]
        public string? CompanyAddress_En { get; set; }

        [BindProperty]
        public string? FacebookUrl { get; set; }

        [BindProperty]
        public string? TwitterUrl { get; set; }

        [BindProperty]
        public string? InstagramUrl { get; set; }

        [BindProperty]
        public string? LinkedInUrl { get; set; }

        [BindProperty]
        public string? YouTubeUrl { get; set; }

        [BindProperty]
        public string? FooterCopyright { get; set; }

        [BindProperty]
        public string? FooterCopyright_En { get; set; }

        [BindProperty]
        public bool ShowHits { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return RedirectToPage("/Login");
            }

            if (!User.HasClaim("Permission", "Settings.View"))
            {
                TempData["ErrorMessage"] = "ليس لديك صلاحية كافية لعرض إعدادات النظام.";
                return RedirectToPage("/Index");
            }

            string? connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                ErrorMessage = "جملة الاتصال بقاعدة البيانات غير مهيأة.";
                return Page();
            }

            await LoadSettingsAsync(connectionString);
            return Page();
        }

        public async Task<IActionResult> OnPostSaveAsync()
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return RedirectToPage("/Login");
            }

            if (!User.HasClaim("Permission", "Settings.Edit"))
            {
                TempData["ErrorMessage"] = "ليس لديك صلاحية كافية لتعديل إعدادات النظام.";
                return RedirectToPage("/Index");
            }

            if (string.IsNullOrWhiteSpace(SiteName))
            {
                ErrorMessage = "اسم الموقع مطلوب.";
                return Page();
            }

            string? connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                ErrorMessage = "جملة الاتصال بقاعدة البيانات غير مهيأة.";
                return Page();
            }

            try
            {
                var (existingLogo, existingFavicon) = await GetExistingBrandingPathsAsync(connectionString);
                var (maxUploadSizeMb, _) = await UploadValidation.LoadSettingsAsync(connectionString);

                if (LogoFile != null && LogoFile.Length > 0)
                {
                    var (webPath, uploadError) = await BrandingHelper.SaveBrandingFileAsync(
                        LogoFile,
                        _environment.WebRootPath,
                        "CMSLogo",
                        BrandingHelper.LogoExtensions,
                        maxUploadSizeMb);

                    if (uploadError != null)
                    {
                        ErrorMessage = uploadError;
                        await ReloadFormStateAsync(existingLogo, existingFavicon);
                        return Page();
                    }

                    Logo = webPath;
                }
                else if (string.IsNullOrWhiteSpace(Logo))
                {
                    Logo = existingLogo;
                }

                if (FaviconFile != null && FaviconFile.Length > 0)
                {
                    var (webPath, uploadError) = await BrandingHelper.SaveBrandingFileAsync(
                        FaviconFile,
                        _environment.WebRootPath,
                        "CMSFavicon",
                        BrandingHelper.FaviconExtensions,
                        maxUploadSizeMb);

                    if (uploadError != null)
                    {
                        ErrorMessage = uploadError;
                        await ReloadFormStateAsync(existingLogo, existingFavicon);
                        return Page();
                    }

                    Favicon = webPath;
                }
                else if (string.IsNullOrWhiteSpace(Favicon))
                {
                    Favicon = existingFavicon;
                }

                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                const string updateQuery = @"
                    UPDATE dbo.Settings
                    SET SiteName = @SiteName,
                        SiteName_En = @SiteName_En,
                        SiteDescription = @SiteDescription,
                        SiteDescription_En = @SiteDescription_En,
                        Logo = @Logo,
                        Favicon = @Favicon,
                        DefaultLanguage = @DefaultLanguage,
                        SupportedLanguages = @SupportedLanguages,
                        Theme = @Theme,
                        AccentColor = @AccentColor,
                        EnableDarkMode = @EnableDarkMode,
                        EnableRTL = @EnableRTL,
                        DateFormat = @DateFormat,
                        TimeFormat = @TimeFormat,
                        DateTimeFormat = @DateTimeFormat,
                        TimeZone = @TimeZone,
                        DefaultPageSize = @DefaultPageSize,
                        MaxUploadSizeMB = @MaxUploadSizeMB,
                        AllowedFileExtensions = @AllowedFileExtensions,
                        EnableSeo = @EnableSeo,
                        EnableCategories = @EnableCategories,
                        EnableMediaLibrary = @EnableMediaLibrary,
                        EnableContentVersioning = @EnableContentVersioning,
                        AutoGenerateSlug = @AutoGenerateSlug,
                        DefaultContentStatus = @DefaultContentStatus,
                        ApiEnabled = @ApiEnabled,
                        GoogleAnalyticsCode = @GoogleAnalyticsCode,
                        GoogleTagManagerCode = @GoogleTagManagerCode,
                        CompanyName = @CompanyName,
                        CompanyName_En = @CompanyName_En,
                        CompanyEmail = @CompanyEmail,
                        CompanyPhone = @CompanyPhone,
                        CompanyAddress = @CompanyAddress,
                        CompanyAddress_En = @CompanyAddress_En,
                        FacebookUrl = @FacebookUrl,
                        TwitterUrl = @TwitterUrl,
                        InstagramUrl = @InstagramUrl,
                        LinkedInUrl = @LinkedInUrl,
                        YouTubeUrl = @YouTubeUrl,
                        FooterCopyright = @FooterCopyright,
                        FooterCopyright_En = @FooterCopyright_En,
                        ShowHits = @ShowHits,
                        UpdatedAt = GETUTCDATE()
                    WHERE Id = 1";

                using var command = new SqlCommand(updateQuery, connection);
                command.Parameters.AddWithValue("@SiteName", SiteName);
                command.Parameters.AddWithValue("@SiteName_En", (object?)SiteName_En ?? DBNull.Value);
                command.Parameters.AddWithValue("@SiteDescription", (object?)SiteDescription ?? DBNull.Value);
                command.Parameters.AddWithValue("@SiteDescription_En", (object?)SiteDescription_En ?? DBNull.Value);
                command.Parameters.AddWithValue("@Logo", (object?)Logo ?? DBNull.Value);
                command.Parameters.AddWithValue("@Favicon", (object?)Favicon ?? DBNull.Value);
                command.Parameters.AddWithValue("@DefaultLanguage", DefaultLanguage);
                command.Parameters.AddWithValue("@SupportedLanguages", SupportedLanguages);
                command.Parameters.AddWithValue("@Theme", Theme);
                command.Parameters.AddWithValue("@AccentColor", (object?)AccentColor ?? DBNull.Value);
                command.Parameters.AddWithValue("@EnableDarkMode", EnableDarkMode);
                command.Parameters.AddWithValue("@EnableRTL", EnableRTL);
                command.Parameters.AddWithValue("@DateFormat", DateFormat);
                command.Parameters.AddWithValue("@TimeFormat", TimeFormat);
                command.Parameters.AddWithValue("@DateTimeFormat", DateTimeFormat);
                command.Parameters.AddWithValue("@TimeZone", TimeZone);
                command.Parameters.AddWithValue("@DefaultPageSize", DefaultPageSize);
                command.Parameters.AddWithValue("@MaxUploadSizeMB", MaxUploadSizeMB);
                command.Parameters.AddWithValue("@AllowedFileExtensions", (object?)AllowedFileExtensions ?? DBNull.Value);
                command.Parameters.AddWithValue("@EnableSeo", EnableSeo);
                command.Parameters.AddWithValue("@EnableCategories", EnableCategories);
                command.Parameters.AddWithValue("@EnableMediaLibrary", EnableMediaLibrary);
                command.Parameters.AddWithValue("@EnableContentVersioning", EnableContentVersioning);
                command.Parameters.AddWithValue("@AutoGenerateSlug", AutoGenerateSlug);
                command.Parameters.AddWithValue("@DefaultContentStatus", DefaultContentStatus);
                command.Parameters.AddWithValue("@ApiEnabled", ApiEnabled);
                command.Parameters.AddWithValue("@GoogleAnalyticsCode", (object?)GoogleAnalyticsCode ?? DBNull.Value);
                command.Parameters.AddWithValue("@GoogleTagManagerCode", (object?)GoogleTagManagerCode ?? DBNull.Value);
                command.Parameters.AddWithValue("@CompanyName", (object?)CompanyName ?? DBNull.Value);
                command.Parameters.AddWithValue("@CompanyName_En", (object?)CompanyName_En ?? DBNull.Value);
                command.Parameters.AddWithValue("@CompanyEmail", (object?)CompanyEmail ?? DBNull.Value);
                command.Parameters.AddWithValue("@CompanyPhone", (object?)CompanyPhone ?? DBNull.Value);
                command.Parameters.AddWithValue("@CompanyAddress", (object?)CompanyAddress ?? DBNull.Value);
                command.Parameters.AddWithValue("@CompanyAddress_En", (object?)CompanyAddress_En ?? DBNull.Value);
                command.Parameters.AddWithValue("@FacebookUrl", (object?)FacebookUrl ?? DBNull.Value);
                command.Parameters.AddWithValue("@TwitterUrl", (object?)TwitterUrl ?? DBNull.Value);
                command.Parameters.AddWithValue("@InstagramUrl", (object?)InstagramUrl ?? DBNull.Value);
                command.Parameters.AddWithValue("@LinkedInUrl", (object?)LinkedInUrl ?? DBNull.Value);
                command.Parameters.AddWithValue("@YouTubeUrl", (object?)YouTubeUrl ?? DBNull.Value);
                command.Parameters.AddWithValue("@FooterCopyright", (object?)FooterCopyright ?? DBNull.Value);
                command.Parameters.AddWithValue("@FooterCopyright_En", (object?)FooterCopyright_En ?? DBNull.Value);
                command.Parameters.AddWithValue("@ShowHits", ShowHits);

                await command.ExecuteNonQueryAsync();
                ApiEnabledMiddleware.InvalidateCache();
                TempData["SuccessMessage"] = "تم حفظ إعدادات النظام بنجاح.";
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"حدث خطأ أثناء حفظ الإعدادات: {ex.Message}";
                await ReloadFormStateAsync();
            }

            return Page();
        }

        private async Task ReloadFormStateAsync(string? logoOverride = null, string? faviconOverride = null)
        {
            string? connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                return;
            }

            await LoadSettingsAsync(connectionString);
            if (logoOverride != null)
            {
                Logo = logoOverride;
            }

            if (faviconOverride != null)
            {
                Favicon = faviconOverride;
            }
        }

        private static async Task<(string? Logo, string? Favicon)> GetExistingBrandingPathsAsync(string connectionString)
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            const string query = "SELECT Logo, Favicon FROM dbo.Settings WHERE Id = 1";
            await using var command = new SqlCommand(query, connection);
            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return (
                    reader.IsDBNull(0) ? null : reader.GetString(0),
                    reader.IsDBNull(1) ? null : reader.GetString(1));
            }

            return (null, null);
        }

        private async Task LoadSettingsAsync(string connectionString)
        {
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            const string selectQuery = "SELECT SiteName, SiteName_En, SiteDescription, SiteDescription_En, Logo, Favicon, DefaultLanguage, SupportedLanguages, Theme, AccentColor, EnableDarkMode, EnableRTL, DateFormat, TimeFormat, DateTimeFormat, TimeZone, DefaultPageSize, MaxUploadSizeMB, AllowedFileExtensions, EnableSeo, EnableCategories, EnableMediaLibrary, EnableContentVersioning, AutoGenerateSlug, DefaultContentStatus, ApiEnabled, GoogleAnalyticsCode, GoogleTagManagerCode, CompanyName, CompanyName_En, CompanyEmail, CompanyPhone, CompanyAddress, CompanyAddress_En, FacebookUrl, TwitterUrl, InstagramUrl, LinkedInUrl, YouTubeUrl, FooterCopyright, FooterCopyright_En, ShowHits FROM dbo.Settings WHERE Id = 1";

            using var command = new SqlCommand(selectQuery, connection);
            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                SiteName = reader.GetString(0);
                SiteName_En = reader.IsDBNull(1) ? null : reader.GetString(1);
                SiteDescription = reader.IsDBNull(2) ? null : reader.GetString(2);
                SiteDescription_En = reader.IsDBNull(3) ? null : reader.GetString(3);
                Logo = reader.IsDBNull(4) ? null : reader.GetString(4);
                Favicon = reader.IsDBNull(5) ? null : reader.GetString(5);
                DefaultLanguage = reader.GetString(6);
                SupportedLanguages = reader.GetString(7);
                Theme = reader.GetString(8);
                AccentColor = reader.IsDBNull(9) ? null : reader.GetString(9);
                EnableDarkMode = reader.GetBoolean(10);
                EnableRTL = reader.GetBoolean(11);
                DateFormat = reader.GetString(12);
                TimeFormat = reader.GetString(13);
                DateTimeFormat = reader.GetString(14);
                TimeZone = reader.GetString(15);
                DefaultPageSize = reader.GetInt32(16);
                MaxUploadSizeMB = reader.GetInt32(17);
                AllowedFileExtensions = reader.IsDBNull(18) ? null : reader.GetString(18);
                EnableSeo = reader.GetBoolean(19);
                EnableCategories = reader.GetBoolean(20);
                EnableMediaLibrary = reader.GetBoolean(21);
                EnableContentVersioning = reader.GetBoolean(22);
                AutoGenerateSlug = reader.GetBoolean(23);
                DefaultContentStatus = reader.GetString(24);
                ApiEnabled = reader.GetBoolean(25);
                GoogleAnalyticsCode = reader.IsDBNull(26) ? null : reader.GetString(26);
                GoogleTagManagerCode = reader.IsDBNull(27) ? null : reader.GetString(27);
                CompanyName = reader.IsDBNull(28) ? null : reader.GetString(28);
                CompanyName_En = reader.IsDBNull(29) ? null : reader.GetString(29);
                CompanyEmail = reader.IsDBNull(30) ? null : reader.GetString(30);
                CompanyPhone = reader.IsDBNull(31) ? null : reader.GetString(31);
                CompanyAddress = reader.IsDBNull(32) ? null : reader.GetString(32);
                CompanyAddress_En = reader.IsDBNull(33) ? null : reader.GetString(33);
                FacebookUrl = reader.IsDBNull(34) ? null : reader.GetString(34);
                TwitterUrl = reader.IsDBNull(35) ? null : reader.GetString(35);
                InstagramUrl = reader.IsDBNull(36) ? null : reader.GetString(36);
                LinkedInUrl = reader.IsDBNull(37) ? null : reader.GetString(37);
                YouTubeUrl = reader.IsDBNull(38) ? null : reader.GetString(38);
                FooterCopyright = reader.IsDBNull(39) ? null : reader.GetString(39);
                FooterCopyright_En = reader.IsDBNull(40) ? null : reader.GetString(40);
                ShowHits = reader.IsDBNull(41) ? true : reader.GetBoolean(41);
            }
        }
    }
}
