using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using TalaPress.Infrastructure;
using TalaPress.Services;

namespace TalaPress.Pages
{
    public class SettingsModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;
        private readonly ISecretProtector _secretProtector;
        private readonly ISmtpEmailService _smtpEmailService;

        public SettingsModel(
            IConfiguration configuration,
            IWebHostEnvironment environment,
            ISecretProtector secretProtector,
            ISmtpEmailService smtpEmailService)
        {
            _configuration = configuration;
            _environment = environment;
            _secretProtector = secretProtector;
            _smtpEmailService = smtpEmailService;
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
        public string? SiteUrl { get; set; }

        [BindProperty]
        public string? Logo { get; set; }

        [BindProperty]
        public IFormFile? LogoFile { get; set; }

        [BindProperty]
        public string? LogoLight { get; set; }

        [BindProperty]
        public IFormFile? LogoLightFile { get; set; }

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
        public string? CompanyMapUrl { get; set; }

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

        [BindProperty]
        public bool SmtpEnabled { get; set; }

        [BindProperty]
        public string? SmtpHost { get; set; }

        [BindProperty]
        public int SmtpPort { get; set; } = 587;

        [BindProperty]
        public bool SmtpUseSsl { get; set; } = true;

        [BindProperty]
        public string? SmtpUsername { get; set; }

        [BindProperty]
        public string? SmtpPassword { get; set; }

        public bool SmtpPasswordIsSet { get; set; }

        [BindProperty]
        public string? SmtpFromEmail { get; set; }

        [BindProperty]
        public string? SmtpFromName { get; set; }

        [BindProperty]
        public string? SmtpTestEmail { get; set; }

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

            if ((SiteDescription?.Length ?? 0) > 250 || (SiteDescription_En?.Length ?? 0) > 250)
            {
                ErrorMessage = "النص التعريفي يجب ألا يتجاوز 250 حرفاً.";
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
                var (existingLogo, existingLogoLight, existingFavicon) = await GetExistingBrandingPathsAsync(connectionString);
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
                        await ReloadFormStateAsync(existingLogo, existingLogoLight, existingFavicon);
                        return Page();
                    }

                    Logo = webPath;
                }
                else if (string.IsNullOrWhiteSpace(Logo))
                {
                    Logo = existingLogo;
                }

                if (LogoLightFile != null && LogoLightFile.Length > 0)
                {
                    var (webPath, uploadError) = await BrandingHelper.SaveBrandingFileAsync(
                        LogoLightFile,
                        _environment.WebRootPath,
                        "CMSLogoLight",
                        BrandingHelper.LogoExtensions,
                        maxUploadSizeMb);

                    if (uploadError != null)
                    {
                        ErrorMessage = uploadError;
                        await ReloadFormStateAsync(existingLogo, existingLogoLight, existingFavicon);
                        return Page();
                    }

                    LogoLight = webPath;
                }
                else if (string.IsNullOrWhiteSpace(LogoLight))
                {
                    LogoLight = existingLogoLight;
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
                        await ReloadFormStateAsync(existingLogo, existingLogoLight, existingFavicon);
                        return Page();
                    }

                    Favicon = webPath;
                }
                else if (string.IsNullOrWhiteSpace(Favicon))
                {
                    Favicon = existingFavicon;
                }

                string? smtpPasswordProtected = await GetExistingSmtpPasswordProtectedAsync(connectionString);
                if (!string.IsNullOrWhiteSpace(SmtpPassword))
                {
                    smtpPasswordProtected = _secretProtector.Protect(SmtpPassword);
                }

                if (SmtpEnabled)
                {
                    if (string.IsNullOrWhiteSpace(SmtpHost) || string.IsNullOrWhiteSpace(SmtpFromEmail))
                    {
                        ErrorMessage = "عند تفعيل SMTP يجب إدخال الخادم وبريد المرسل.";
                        await ReloadFormStateAsync(existingLogo, existingLogoLight, existingFavicon);
                        return Page();
                    }
                    if (string.IsNullOrWhiteSpace(smtpPasswordProtected) && string.IsNullOrWhiteSpace(SmtpUsername) == false)
                    {
                        ErrorMessage = "كلمة مرور SMTP مطلوبة عند استخدام اسم مستخدم.";
                        await ReloadFormStateAsync(existingLogo, existingLogoLight, existingFavicon);
                        return Page();
                    }
                }

                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                const string updateQuery = @"
                    UPDATE dbo.Settings
                    SET SiteName = @SiteName,
                        SiteName_En = @SiteName_En,
                        SiteDescription = @SiteDescription,
                        SiteDescription_En = @SiteDescription_En,
                        SiteUrl = @SiteUrl,
                        Logo = @Logo,
                        LogoLight = @LogoLight,
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
                        CompanyMapUrl = @CompanyMapUrl,
                        FacebookUrl = @FacebookUrl,
                        TwitterUrl = @TwitterUrl,
                        InstagramUrl = @InstagramUrl,
                        LinkedInUrl = @LinkedInUrl,
                        YouTubeUrl = @YouTubeUrl,
                        FooterCopyright = @FooterCopyright,
                        FooterCopyright_En = @FooterCopyright_En,
                        ShowHits = @ShowHits,
                        SmtpEnabled = @SmtpEnabled,
                        SmtpHost = @SmtpHost,
                        SmtpPort = @SmtpPort,
                        SmtpUseSsl = @SmtpUseSsl,
                        SmtpUsername = @SmtpUsername,
                        SmtpPasswordProtected = @SmtpPasswordProtected,
                        SmtpFromEmail = @SmtpFromEmail,
                        SmtpFromName = @SmtpFromName,
                        UpdatedAt = GETUTCDATE()
                    WHERE Id = 1";

                using var command = new SqlCommand(updateQuery, connection);
                command.Parameters.AddWithValue("@SiteName", SiteName);
                command.Parameters.AddWithValue("@SiteName_En", (object?)SiteName_En ?? DBNull.Value);
                command.Parameters.AddWithValue("@SiteDescription", (object?)SiteDescription ?? DBNull.Value);
                command.Parameters.AddWithValue("@SiteDescription_En", (object?)SiteDescription_En ?? DBNull.Value);
                command.Parameters.AddWithValue("@SiteUrl", (object?)SiteUrl ?? DBNull.Value);
                command.Parameters.AddWithValue("@Logo", (object?)Logo ?? DBNull.Value);
                command.Parameters.AddWithValue("@LogoLight", (object?)LogoLight ?? DBNull.Value);
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
                command.Parameters.AddWithValue("@CompanyMapUrl", (object?)CompanyMapUrl ?? DBNull.Value);
                command.Parameters.AddWithValue("@FacebookUrl", (object?)FacebookUrl ?? DBNull.Value);
                command.Parameters.AddWithValue("@TwitterUrl", (object?)TwitterUrl ?? DBNull.Value);
                command.Parameters.AddWithValue("@InstagramUrl", (object?)InstagramUrl ?? DBNull.Value);
                command.Parameters.AddWithValue("@LinkedInUrl", (object?)LinkedInUrl ?? DBNull.Value);
                command.Parameters.AddWithValue("@YouTubeUrl", (object?)YouTubeUrl ?? DBNull.Value);
                command.Parameters.AddWithValue("@FooterCopyright", (object?)FooterCopyright ?? DBNull.Value);
                command.Parameters.AddWithValue("@FooterCopyright_En", (object?)FooterCopyright_En ?? DBNull.Value);
                command.Parameters.AddWithValue("@ShowHits", ShowHits);
                command.Parameters.AddWithValue("@SmtpEnabled", SmtpEnabled);
                command.Parameters.AddWithValue("@SmtpHost", (object?)SmtpHost ?? DBNull.Value);
                command.Parameters.AddWithValue("@SmtpPort", SmtpPort > 0 ? SmtpPort : 587);
                command.Parameters.AddWithValue("@SmtpUseSsl", SmtpUseSsl);
                command.Parameters.AddWithValue("@SmtpUsername", (object?)SmtpUsername ?? DBNull.Value);
                command.Parameters.AddWithValue("@SmtpPasswordProtected", (object?)smtpPasswordProtected ?? DBNull.Value);
                command.Parameters.AddWithValue("@SmtpFromEmail", (object?)SmtpFromEmail ?? DBNull.Value);
                command.Parameters.AddWithValue("@SmtpFromName", (object?)SmtpFromName ?? DBNull.Value);

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

        public async Task<IActionResult> OnPostTestSmtpAsync()
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return RedirectToPage("/Login");
            }

            if (!User.HasClaim("Permission", "Settings.Edit"))
            {
                TempData["ErrorMessage"] = "ليس لديك صلاحية.";
                return RedirectToPage();
            }

            string? connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                TempData["ErrorMessage"] = "قاعدة البيانات غير مهيأة.";
                return RedirectToPage();
            }

            await LoadSettingsAsync(connectionString);

            if (!SmtpEnabled)
            {
                TempData["ErrorMessage"] = "SMTP غير مفعّل. احفظ الإعدادات أولاً.";
                return RedirectToPage();
            }

            string to = string.IsNullOrWhiteSpace(SmtpTestEmail) ? (SmtpFromEmail ?? CompanyEmail ?? "") : SmtpTestEmail;
            var (ok, error) = await _smtpEmailService.SendTestAsync(to);
            TempData[ok ? "SuccessMessage" : "ErrorMessage"] = ok
                ? "تم إرسال رسالة الاختبار بنجاح."
                : $"فشل إرسال الاختبار: {error}";

            return RedirectToPage();
        }

        private async Task<string?> GetExistingSmtpPasswordProtectedAsync(string connectionString)
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            await using var cmd = new SqlCommand("SELECT SmtpPasswordProtected FROM dbo.Settings WHERE Id = 1", connection);
            object? result = await cmd.ExecuteScalarAsync();
            return result == null || result == DBNull.Value ? null : result.ToString();
        }

        private async Task ReloadFormStateAsync(string? logoOverride = null, string? logoLightOverride = null, string? faviconOverride = null)
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

            if (logoLightOverride != null)
            {
                LogoLight = logoLightOverride;
            }

            if (faviconOverride != null)
            {
                Favicon = faviconOverride;
            }
        }

        private static async Task<(string? Logo, string? LogoLight, string? Favicon)> GetExistingBrandingPathsAsync(string connectionString)
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            const string query = "SELECT Logo, LogoLight, Favicon FROM dbo.Settings WHERE Id = 1";
            await using var command = new SqlCommand(query, connection);
            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return (
                    reader.IsDBNull(0) ? null : reader.GetString(0),
                    reader.IsDBNull(1) ? null : reader.GetString(1),
                    reader.IsDBNull(2) ? null : reader.GetString(2));
            }

            return (null, null, null);
        }

        private async Task LoadSettingsAsync(string connectionString)
        {
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            const string selectQuery = @"SELECT SiteName, SiteName_En, SiteDescription, SiteDescription_En, SiteUrl, Logo, LogoLight, Favicon,
                DefaultLanguage, SupportedLanguages, Theme, AccentColor, EnableDarkMode, EnableRTL, DateFormat, TimeFormat, DateTimeFormat,
                TimeZone, DefaultPageSize, MaxUploadSizeMB, AllowedFileExtensions, EnableSeo, EnableCategories, EnableMediaLibrary,
                EnableContentVersioning, AutoGenerateSlug, DefaultContentStatus, ApiEnabled, GoogleAnalyticsCode, GoogleTagManagerCode,
                CompanyName, CompanyName_En, CompanyEmail, CompanyPhone, CompanyAddress, CompanyAddress_En, CompanyMapUrl,
                FacebookUrl, TwitterUrl, InstagramUrl, LinkedInUrl, YouTubeUrl, FooterCopyright, FooterCopyright_En, ShowHits,
                SmtpEnabled, SmtpHost, SmtpPort, SmtpUseSsl, SmtpUsername, SmtpPasswordProtected, SmtpFromEmail, SmtpFromName
                FROM dbo.Settings WHERE Id = 1";

            using var command = new SqlCommand(selectQuery, connection);
            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                SiteName = reader.GetString(0);
                SiteName_En = reader.IsDBNull(1) ? null : reader.GetString(1);
                SiteDescription = reader.IsDBNull(2) ? null : reader.GetString(2);
                SiteDescription_En = reader.IsDBNull(3) ? null : reader.GetString(3);
                SiteUrl = reader.IsDBNull(4) ? null : reader.GetString(4);
                Logo = reader.IsDBNull(5) ? null : reader.GetString(5);
                LogoLight = reader.IsDBNull(6) ? null : reader.GetString(6);
                Favicon = reader.IsDBNull(7) ? null : reader.GetString(7);
                DefaultLanguage = reader.GetString(8);
                SupportedLanguages = reader.GetString(9);
                Theme = reader.GetString(10);
                AccentColor = reader.IsDBNull(11) ? null : reader.GetString(11);
                EnableDarkMode = reader.GetBoolean(12);
                EnableRTL = reader.GetBoolean(13);
                DateFormat = reader.GetString(14);
                TimeFormat = reader.GetString(15);
                DateTimeFormat = reader.GetString(16);
                TimeZone = reader.GetString(17);
                DefaultPageSize = reader.GetInt32(18);
                MaxUploadSizeMB = reader.GetInt32(19);
                AllowedFileExtensions = reader.IsDBNull(20) ? null : reader.GetString(20);
                EnableSeo = reader.GetBoolean(21);
                EnableCategories = reader.GetBoolean(22);
                EnableMediaLibrary = reader.GetBoolean(23);
                EnableContentVersioning = reader.GetBoolean(24);
                AutoGenerateSlug = reader.GetBoolean(25);
                DefaultContentStatus = reader.GetString(26);
                ApiEnabled = reader.GetBoolean(27);
                GoogleAnalyticsCode = reader.IsDBNull(28) ? null : reader.GetString(28);
                GoogleTagManagerCode = reader.IsDBNull(29) ? null : reader.GetString(29);
                CompanyName = reader.IsDBNull(30) ? null : reader.GetString(30);
                CompanyName_En = reader.IsDBNull(31) ? null : reader.GetString(31);
                CompanyEmail = reader.IsDBNull(32) ? null : reader.GetString(32);
                CompanyPhone = reader.IsDBNull(33) ? null : reader.GetString(33);
                CompanyAddress = reader.IsDBNull(34) ? null : reader.GetString(34);
                CompanyAddress_En = reader.IsDBNull(35) ? null : reader.GetString(35);
                CompanyMapUrl = reader.IsDBNull(36) ? null : reader.GetString(36);
                FacebookUrl = reader.IsDBNull(37) ? null : reader.GetString(37);
                TwitterUrl = reader.IsDBNull(38) ? null : reader.GetString(38);
                InstagramUrl = reader.IsDBNull(39) ? null : reader.GetString(39);
                LinkedInUrl = reader.IsDBNull(40) ? null : reader.GetString(40);
                YouTubeUrl = reader.IsDBNull(41) ? null : reader.GetString(41);
                FooterCopyright = reader.IsDBNull(42) ? null : reader.GetString(42);
                FooterCopyright_En = reader.IsDBNull(43) ? null : reader.GetString(43);
                ShowHits = reader.IsDBNull(44) ? true : reader.GetBoolean(44);
                if (reader.FieldCount > 45)
                {
                    SmtpEnabled = !reader.IsDBNull(45) && reader.GetBoolean(45);
                    SmtpHost = reader.IsDBNull(46) ? null : reader.GetString(46);
                    SmtpPort = reader.IsDBNull(47) ? 587 : reader.GetInt32(47);
                    SmtpUseSsl = reader.IsDBNull(48) || reader.GetBoolean(48);
                    SmtpUsername = reader.IsDBNull(49) ? null : reader.GetString(49);
                    SmtpPasswordIsSet = !reader.IsDBNull(50) && !string.IsNullOrEmpty(reader.GetString(50));
                    SmtpFromEmail = reader.IsDBNull(51) ? null : reader.GetString(51);
                    SmtpFromName = reader.IsDBNull(52) ? null : reader.GetString(52);
                }
            }
        }
    }
}
