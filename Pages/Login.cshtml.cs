using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;

namespace TalaPress.Pages
{
    public class LoginModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;
        private readonly PasswordHasher<string> _passwordHasher = new PasswordHasher<string>();

        public LoginModel(IConfiguration configuration, IWebHostEnvironment environment)
        {
            _configuration = configuration;
            _environment = environment;
        }

        [BindProperty]
        public string UsernameOrEmail { get; set; } = string.Empty;

        [BindProperty]
        public string Password { get; set; } = string.Empty;

        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            // Auto-seed a default user if none exists in the database
            await EnsureDefaultUserAsync();
            return Page();
        }

        public async Task<IActionResult> OnGetLogoutAsync()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToPage("/Login");
        }

        public async Task<IActionResult> OnPostAsync()
        {
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
                ErrorMessage = $"خطأ في الاتصال بقاعدة البيانات: {ex.Message}";
                return Page();
            }

            ErrorMessage = "اسم المستخدم أو كلمة المرور غير صحيحة.";
            return Page();
        }

        private async Task EnsureDefaultUserAsync()
        {
            if (!_environment.IsDevelopment())
            {
                return;
            }

            string? connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString)) return;

            try
            {
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                // Check if any users exist
                string checkQuery = "SELECT COUNT(*) FROM dbo.Users";
                using var checkCmd = new SqlCommand(checkQuery, connection);
                int userCount = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());

                if (userCount == 0)
                {
                    // Create default super admin user: admin / admin
                    string username = "admin";
                    string email = "admin@talapress.com";
                    string hashedPassword = _passwordHasher.HashPassword(username, "admin");

                    // 1. Insert default user
                    string insertUserQuery = @"
                        INSERT INTO dbo.Users (Username, Email, PasswordHash, FullName, IsActive, CreatedAt)
                        VALUES (@Username, @Email, @PasswordHash, @FullName, 1, GETUTCDATE());
                        SELECT SCOPE_IDENTITY();";

                    using var insertCmd = new SqlCommand(insertUserQuery, connection);
                    insertCmd.Parameters.AddWithValue("@Username", username);
                    insertCmd.Parameters.AddWithValue("@Email", email);
                    insertCmd.Parameters.AddWithValue("@PasswordHash", hashedPassword);
                    insertCmd.Parameters.AddWithValue("@FullName", "Super Admin");

                    object? result = await insertCmd.ExecuteScalarAsync();
                    if (result != null && result != DBNull.Value)
                    {
                        long newUserId = Convert.ToInt64(result);

                        // 2. Fetch Super Administrator role ID
                        string getRoleQuery = "SELECT Id FROM dbo.Roles WHERE Name_En = 'Super Administrator'";
                        using var getRoleCmd = new SqlCommand(getRoleQuery, connection);
                        object? roleIdObj = await getRoleCmd.ExecuteScalarAsync();

                        if (roleIdObj != null && roleIdObj != DBNull.Value)
                        {
                            long roleId = Convert.ToInt64(roleIdObj);

                            // 3. Map user to the Super Administrator role
                            string insertUserRoleQuery = "INSERT INTO dbo.UserRoles (UserId, RoleId) VALUES (@UserId, @RoleId)";
                            using var insertUserRoleCmd = new SqlCommand(insertUserRoleQuery, connection);
                            insertUserRoleCmd.Parameters.AddWithValue("@UserId", newUserId);
                            insertUserRoleCmd.Parameters.AddWithValue("@RoleId", roleId);
                            await insertUserRoleCmd.ExecuteNonQueryAsync();
                        }
                    }
                }
            }
            catch
            {
                // Silently handle if tables or DB do not exist yet during app load
            }
        }
    }
}
