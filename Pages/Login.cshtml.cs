using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace TalaPress.Pages
{
    [EnableRateLimiting("login")]
    public class LoginModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<LoginModel> _logger;
        private readonly PasswordHasher<string> _passwordHasher = new PasswordHasher<string>();

        public LoginModel(IConfiguration configuration, ILogger<LoginModel> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        [BindProperty]
        public string UsernameOrEmail { get; set; } = string.Empty;

        [BindProperty]
        public string Password { get; set; } = string.Empty;

        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            await DisableLegacyDefaultUserAsync();
            return Page();
        }

        public async Task<IActionResult> OnGetLogoutAsync()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToPage("/Login");
        }

        public async Task<IActionResult> OnPostAsync()
        {
            await DisableLegacyDefaultUserAsync();

            if (!ModelState.IsValid)
            {
                return Page();
            }

            string? connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                ErrorMessage = "جملة الاتصال بقاعدة البيانات غير مهيأة في appsettings.json";
                return Page();
            }

            try
            {
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                long userId = 0;
                string username = "";
                string email = "";
                string storedHash = "";
                string fullName = "";
                bool isActive = false;
                bool userFound = false;

                // Select the user by Username or Email
                string query = "SELECT Id, Username, Email, PasswordHash, FullName, IsActive FROM dbo.Users WHERE Username = @UsernameOrEmail OR Email = @UsernameOrEmail";
                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@UsernameOrEmail", UsernameOrEmail);
                    using var reader = await command.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        userId = reader.GetInt64(0);
                        username = reader.GetString(1);
                        email = reader.GetString(2);
                        storedHash = reader.GetString(3);
                        fullName = reader.IsDBNull(4) ? username : reader.GetString(4);
                        isActive = reader.GetBoolean(5);
                        userFound = true;
                    }
                }

                if (userFound)
                {
                    if (!isActive)
                    {
                        ErrorMessage = "هذا الحساب معطل حالياً.";
                        return Page();
                    }

                    // Verify password using standard ASP.NET Core Identity PasswordHasher
                    var result = _passwordHasher.VerifyHashedPassword(UsernameOrEmail, storedHash, Password);
                    if (result == PasswordVerificationResult.Success || result == PasswordVerificationResult.SuccessRehashNeeded)
                    {
                        // Fetch distinct permission codes for this user
                        var permissions = new List<string>();
                        string permQuery = @"
                            SELECT DISTINCT p.Code 
                            FROM dbo.UserRoles ur
                            JOIN dbo.RolePermissions rp ON ur.RoleId = rp.RoleId
                            JOIN dbo.Permissions p ON rp.PermissionId = p.Id
                            WHERE ur.UserId = @UserId";
                        
                        using (var permCmd = new SqlCommand(permQuery, connection))
                        {
                            permCmd.Parameters.AddWithValue("@UserId", userId);
                            using var permReader = await permCmd.ExecuteReaderAsync();
                            while (await permReader.ReadAsync())
                            {
                                permissions.Add(permReader.GetString(0));
                            }
                        }

                        // Create claims
                        var claims = new List<Claim>
                        {
                            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                            new Claim(ClaimTypes.Name, username),
                            new Claim(ClaimTypes.Email, email),
                            new Claim("FullName", fullName)
                        };

                        foreach (var perm in permissions)
                        {
                            claims.Add(new Claim("Permission", perm));
                        }

                        // Sign in the user
                        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                        var authProperties = new AuthenticationProperties
                        {
                            IsPersistent = true,
                            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(2)
                        };

                        await HttpContext.SignInAsync(
                            CookieAuthenticationDefaults.AuthenticationScheme,
                            new ClaimsPrincipal(claimsIdentity),
                            authProperties);

                        return RedirectToPage("/Index");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TalaPress login failed due to an internal authentication error.");
                ErrorMessage = "تعذر إتمام تسجيل الدخول حالياً. يرجى المحاولة لاحقاً.";
                return Page();
            }

            ErrorMessage = "اسم المستخدم أو كلمة المرور غير صحيحة.";
            return Page();
        }

        private async Task DisableLegacyDefaultUserAsync()
        {
            string? connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString)) return;

            try
            {
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                const string query = "SELECT TOP (1) Id, PasswordHash FROM dbo.Users WHERE Username = 'admin' AND Email = 'admin@talapress.com' AND IsActive = 1";
                long? userId = null;
                string? passwordHash = null;
                using (var command = new SqlCommand(query, connection))
                {
                    using var reader = await command.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        userId = reader.GetInt64(0);
                        passwordHash = reader.GetString(1);
                    }
                }

                if (userId.HasValue && passwordHash is not null
                    && _passwordHasher.VerifyHashedPassword("admin", passwordHash, "admin") != PasswordVerificationResult.Failed)
                {
                    using var disableCommand = new SqlCommand("UPDATE dbo.Users SET IsActive = 0, UpdatedAt = GETUTCDATE() WHERE Id = @Id", connection);
                    disableCommand.Parameters.AddWithValue("@Id", userId.Value);
                    await disableCommand.ExecuteNonQueryAsync();
                    _logger.LogWarning("Disabled the legacy TalaPress admin account because it still used the known default credential.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unable to check for the legacy TalaPress default account.");
            }
        }
    }
}
