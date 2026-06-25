using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Identity;

namespace TalaPress.Pages
{
    public class UsersModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly PasswordHasher<string> _passwordHasher = new PasswordHasher<string>();

        public UsersModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // List of all platform users
        public List<UserViewModel> UserList { get; set; } = new();

        // List of all available roles for checkboxes
        public List<RoleSelectionViewModel> AvailableRoles { get; set; } = new();

        [BindProperty]
        public long Id { get; set; }

        [BindProperty]
        public string Username { get; set; } = string.Empty;

        [BindProperty]
        public string Email { get; set; } = string.Empty;

        [BindProperty]
        public string? FullName { get; set; }

        [BindProperty]
        public string? Password { get; set; }

        [BindProperty]
        public bool IsActive { get; set; } = true;

        [BindProperty]
        public List<long> SelectedRoleIds { get; set; } = new();

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        // Selected User ID to load into the form
        public long SelectedUserId { get; set; }

        public async Task<IActionResult> OnGetAsync(long? selectedId)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return RedirectToPage("/Login");
            }

            if (!User.HasClaim("Permission", "Users.View"))
            {
                TempData["ErrorMessage"] = "ليس لديك صلاحية كافية لعرض صفحة إدارة المستخدمين.";
                return RedirectToPage("/Index");
            }

            string? connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                ErrorMessage = "جملة الاتصال بقاعدة البيانات غير مهيأة.";
                return Page();
            }

            await LoadUsersAndRolesAsync(connectionString);

            if (selectedId.HasValue && selectedId.Value > 0)
            {
                SelectedUserId = selectedId.Value;
                var selectedUser = UserList.FirstOrDefault(u => u.Id == selectedId.Value);
                if (selectedUser != null)
                {
                    Id = selectedUser.Id;
                    Username = selectedUser.Username;
                    Email = selectedUser.Email;
                    FullName = selectedUser.FullName;
                    IsActive = selectedUser.IsActive;
                    SelectedRoleIds = selectedUser.Roles.Select(r => r.Id).ToList();
                }
            }

            return Page();
        }

        public async Task<IActionResult> OnPostSaveAsync()
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return RedirectToPage("/Login");
            }

            bool canSave = (Id == 0)
                ? User.HasClaim("Permission", "Users.Create")
                : User.HasClaim("Permission", "Users.Edit");

            if (!canSave)
            {
                TempData["ErrorMessage"] = "ليس لديك صلاحية كافية لحفظ بيانات حسابات المستخدمين.";
                return RedirectToPage("/Index");
            }

            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Email))
            {
                ErrorMessage = "اسم المستخدم والبريد الإلكتروني مطلوبان.";
                return await OnGetReloadAsync();
            }

            // Clean inputs
            Username = Username.Trim();
            Email = Email.Trim();
            FullName = FullName?.Trim();

            string? connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                ErrorMessage = "جملة الاتصال بقاعدة البيانات غير مهيأة.";
                return await OnGetReloadAsync();
            }

            try
            {
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                // 1. Unique validations
                string checkQuery = "SELECT Id FROM dbo.Users WHERE (Username = @Username OR Email = @Email) AND Id != @Id";
                using (var checkCmd = new SqlCommand(checkQuery, connection))
                {
                    checkCmd.Parameters.AddWithValue("@Username", Username);
                    checkCmd.Parameters.AddWithValue("@Email", Email);
                    checkCmd.Parameters.AddWithValue("@Id", Id);

                    var existingVal = await checkCmd.ExecuteScalarAsync();
                    if (existingVal != null)
                    {
                        ErrorMessage = "اسم المستخدم أو البريد الإلكتروني مستخدم بالفعل من قبل حساب آخر.";
                        return await OnGetReloadAsync();
                    }
                }

                // Protect super admin user from deactivation
                if (Id == 1 || Username.ToLower() == "admin")
                {
                    IsActive = true; // Force active
                }

                using var transaction = connection.BeginTransaction();
                try
                {
                    long userId = Id;

                    if (userId == 0)
                    {
                        // Add mode: Password is required
                        if (string.IsNullOrWhiteSpace(Password))
                        {
                            ErrorMessage = "كلمة المرور مطلوبة لإنشاء مستخدم جديد.";
                            transaction.Rollback();
                            return await OnGetReloadAsync();
                        }

                        string hashedPassword = _passwordHasher.HashPassword(Username, Password);

                        string insertQuery = @"
                            INSERT INTO dbo.Users (Username, Email, PasswordHash, FullName, IsActive, CreatedAt)
                            VALUES (@Username, @Email, @PasswordHash, @FullName, @IsActive, GETUTCDATE());
                            SELECT SCOPE_IDENTITY();";

                        using var command = new SqlCommand(insertQuery, connection, transaction);
                        command.Parameters.AddWithValue("@Username", Username);
                        command.Parameters.AddWithValue("@Email", Email);
                        command.Parameters.AddWithValue("@PasswordHash", hashedPassword);
                        command.Parameters.AddWithValue("@FullName", (object?)FullName ?? DBNull.Value);
                        command.Parameters.AddWithValue("@IsActive", IsActive);

                        userId = Convert.ToInt64(await command.ExecuteScalarAsync());
                        SuccessMessage = "تم إضافة المستخدم بنجاح.";
                    }
                    else
                    {
                        // Edit mode: Update user details
                        string updateQuery;
                        bool hasNewPassword = !string.IsNullOrWhiteSpace(Password);

                        if (hasNewPassword)
                        {
                            updateQuery = @"
                                UPDATE dbo.Users 
                                SET Username = @Username, Email = @Email, PasswordHash = @PasswordHash, 
                                    FullName = @FullName, IsActive = @IsActive, UpdatedAt = GETUTCDATE()
                                WHERE Id = @Id";
                        }
                        else
                        {
                            updateQuery = @"
                                UPDATE dbo.Users 
                                SET Username = @Username, Email = @Email, 
                                    FullName = @FullName, IsActive = @IsActive, UpdatedAt = GETUTCDATE()
                                WHERE Id = @Id";
                        }

                        using var command = new SqlCommand(updateQuery, connection, transaction);
                        command.Parameters.AddWithValue("@Id", userId);
                        command.Parameters.AddWithValue("@Username", Username);
                        command.Parameters.AddWithValue("@Email", Email);
                        command.Parameters.AddWithValue("@FullName", (object?)FullName ?? DBNull.Value);
                        command.Parameters.AddWithValue("@IsActive", IsActive);

                        if (hasNewPassword)
                        {
                            string hashedPassword = _passwordHasher.HashPassword(Username, Password!);
                            command.Parameters.AddWithValue("@PasswordHash", hashedPassword);
                        }

                        await command.ExecuteNonQueryAsync();
                        SuccessMessage = "تم تحديث بيانات المستخدم بنجاح.";
                    }

                    // 2. Synchronize Roles mapping
                    // Delete existing role mapping
                    string deleteRolesQuery = "DELETE FROM dbo.UserRoles WHERE UserId = @UserId";
                    using (var deleteCmd = new SqlCommand(deleteRolesQuery, connection, transaction))
                    {
                        deleteCmd.Parameters.AddWithValue("@UserId", userId);
                        await deleteCmd.ExecuteNonQueryAsync();
                    }

                    // Re-insert mapped roles
                    if (userId == 1)
                    {
                        // Ensure Super Admin user (Id = 1) always keeps Super Administrator role (RoleId = 1)
                        if (SelectedRoleIds == null) SelectedRoleIds = new List<long>();
                        if (!SelectedRoleIds.Contains(1))
                        {
                            SelectedRoleIds.Add(1);
                        }
                    }

                    if (SelectedRoleIds != null && SelectedRoleIds.Count > 0)
                    {
                        string insertRoleQuery = "INSERT INTO dbo.UserRoles (UserId, RoleId, CreatedAt) VALUES (@UserId, @RoleId, GETUTCDATE())";
                        foreach (var roleId in SelectedRoleIds)
                        {
                            using var insertCmd = new SqlCommand(insertRoleQuery, connection, transaction);
                            insertCmd.Parameters.AddWithValue("@UserId", userId);
                            insertCmd.Parameters.AddWithValue("@RoleId", roleId);
                            await insertCmd.ExecuteNonQueryAsync();
                        }
                    }

                    await transaction.CommitAsync();
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    ErrorMessage = $"حدث خطأ أثناء حفظ بيانات الحساب: {ex.Message}";
                    return await OnGetReloadAsync();
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"فشل الاتصال بقاعدة البيانات: {ex.Message}";
                return await OnGetReloadAsync();
            }

            return RedirectToPage(new { selectedId = Id > 0 ? (long?)Id : null });
        }

        public async Task<IActionResult> OnPostDeleteAsync(long id)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return RedirectToPage("/Login");
            }

            if (!User.HasClaim("Permission", "Users.Delete"))
            {
                TempData["ErrorMessage"] = "ليس لديك صلاحية كافية لحذف حسابات المستخدمين.";
                return RedirectToPage("/Index");
            }

            if (id == 1)
            {
                ErrorMessage = "لا يمكن حذف حساب المسؤول العام الأساسي (admin).";
                return RedirectToPage();
            }

            string? connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                ErrorMessage = "جملة الاتصال بقاعدة البيانات غير مهيأة.";
                return RedirectToPage();
            }

            try
            {
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                // Delete the user (UserRoles cascades automatically due to FK CASCADE)
                string deleteQuery = "DELETE FROM dbo.Users WHERE Id = @Id";
                using var deleteCmd = new SqlCommand(deleteQuery, connection);
                deleteCmd.Parameters.AddWithValue("@Id", id);
                await deleteCmd.ExecuteNonQueryAsync();

                SuccessMessage = "تم حذف المستخدم بنجاح.";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"حدث خطأ أثناء حذف حساب المستخدم: {ex.Message}";
            }

            return RedirectToPage();
        }

        private async Task<IActionResult> OnGetReloadAsync()
        {
            string? connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (!string.IsNullOrEmpty(connectionString))
            {
                await LoadUsersAndRolesAsync(connectionString);
            }
            return Page();
        }

        private async Task LoadUsersAndRolesAsync(string connectionString)
        {
            UserList.Clear();
            AvailableRoles.Clear();

            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            // 1. Load available Roles for the checkboxes
            string rolesQuery = "SELECT Id, Name, Name_En FROM dbo.Roles WHERE IsActive = 1 ORDER BY Name ASC";
            using (var rolesCmd = new SqlCommand(rolesQuery, connection))
            using (var reader = await rolesCmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    AvailableRoles.Add(new RoleSelectionViewModel
                    {
                        Id = reader.GetInt64(0),
                        Name = reader.GetString(1),
                        Name_En = reader.GetString(2)
                    });
                }
            }

            // 2. Load User -> Roles mapping
            var userRolesDict = new Dictionary<long, List<UserRoleInfo>>();
            string userRolesQuery = "SELECT ur.UserId, r.Id, r.Name, r.Name_En FROM dbo.UserRoles ur JOIN dbo.Roles r ON ur.RoleId = r.Id";
            using (var urCmd = new SqlCommand(userRolesQuery, connection))
            using (var reader = await urCmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    long userId = reader.GetInt64(0);
                    var role = new UserRoleInfo
                    {
                        Id = reader.GetInt64(1),
                        Name = reader.GetString(2),
                        Name_En = reader.GetString(3)
                    };

                    if (!userRolesDict.ContainsKey(userId))
                    {
                        userRolesDict[userId] = new List<UserRoleInfo>();
                    }
                    userRolesDict[userId].Add(role);
                }
            }

            // 3. Load Users
            string usersQuery = "SELECT Id, Username, Email, FullName, IsActive, CreatedAt FROM dbo.Users ORDER BY CreatedAt DESC";
            using (var usersCmd = new SqlCommand(usersQuery, connection))
            using (var reader = await usersCmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    long id = reader.GetInt64(0);
                    var user = new UserViewModel
                    {
                        Id = id,
                        Username = reader.GetString(1),
                        Email = reader.GetString(2),
                        FullName = reader.IsDBNull(3) ? null : reader.GetString(3),
                        IsActive = reader.GetBoolean(4),
                        CreatedAt = reader.GetDateTime(5),
                        Roles = userRolesDict.ContainsKey(id) ? userRolesDict[id] : new List<UserRoleInfo>()
                    };

                    UserList.Add(user);
                }
            }
        }
    }

    public class UserViewModel
    {
        public long Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? FullName { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<UserRoleInfo> Roles { get; set; } = new();
    }

    public class UserRoleInfo
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Name_En { get; set; } = string.Empty;
    }

    public class RoleSelectionViewModel
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Name_En { get; set; } = string.Empty;
    }
}
