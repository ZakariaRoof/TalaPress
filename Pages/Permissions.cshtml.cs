using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace TalaPress.Pages
{
    public class PermissionsModel : PageModel
    {
        private readonly IConfiguration _configuration;

        public PermissionsModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // List of all system roles
        public List<RoleViewModel> RoleList { get; set; } = new();

        // List of all master permissions grouped by module
        public Dictionary<string, List<PermissionViewModel>> GroupedPermissions { get; set; } = new();

        // Set of permission IDs assigned to the currently selected role
        public HashSet<long> SelectedRolePermissionIds { get; set; } = new();

        [BindProperty]
        public long Id { get; set; }

        [BindProperty]
        public string Name { get; set; } = string.Empty;

        [BindProperty]
        public string Name_En { get; set; } = string.Empty;

        [BindProperty]
        public string? Description { get; set; }

        [BindProperty]
        public string? Description_En { get; set; }

        [BindProperty]
        public List<long> SelectedPermissionIds { get; set; } = new();

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        // Selected Role ID for loading into the form
        public long SelectedRoleId { get; set; }

        public async Task<IActionResult> OnGetAsync(long? selectedId)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return RedirectToPage("/Login");
            }

            if (!User.HasClaim("Permission", "Roles.View") && !User.HasClaim("Permission", "Permissions.View"))
            {
                TempData["ErrorMessage"] = "ليس لديك صلاحية كافية لعرض صفحة الأدوار والصلاحيات.";
                return RedirectToPage("/Index");
            }

            string? connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                ErrorMessage = "جملة الاتصال بقاعدة البيانات غير مهيأة.";
                return Page();
            }

            await LoadRolesAndPermissionsAsync(connectionString);

            if (selectedId.HasValue && selectedId.Value > 0)
            {
                SelectedRoleId = selectedId.Value;
                var selectedRole = RoleList.FirstOrDefault(r => r.Id == selectedId.Value);
                if (selectedRole != null)
                {
                    Id = selectedRole.Id;
                    Name = selectedRole.Name;
                    Name_En = selectedRole.Name_En;
                    Description = selectedRole.Description;
                    Description_En = selectedRole.Description_En;

                    // Load permission IDs assigned to this role
                    await LoadRolePermissionsAsync(connectionString, selectedRole.Id);
                }
            }

            return Page();
        }

        public async Task<IActionResult> OnPostSaveRoleAsync()
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return RedirectToPage("/Login");
            }

            bool canSave = (Id == 0)
                ? User.HasClaim("Permission", "Roles.Create")
                : User.HasClaim("Permission", "Roles.Edit");

            if (!canSave)
            {
                TempData["ErrorMessage"] = "ليس لديك صلاحية كافية لحفظ الأدوار والصلاحيات.";
                return RedirectToPage("/Index");
            }

            if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Name_En))
            {
                ErrorMessage = "اسم الدور بالعربية والإنجليزية مطلوبان.";
                return await OnGetReloadAsync();
            }

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

                using var transaction = connection.BeginTransaction();

                try
                {
                    long roleId = Id;
                    bool isSystem = false;

                    if (roleId > 0)
                    {
                        // Check if it is a system role
                        string checkSystemQuery = "SELECT IsSystem FROM dbo.Roles WHERE Id = @Id";
                        using var checkSystemCmd = new SqlCommand(checkSystemQuery, connection, transaction);
                        checkSystemCmd.Parameters.AddWithValue("@Id", roleId);
                        var systemVal = await checkSystemCmd.ExecuteScalarAsync();
                        isSystem = systemVal != null && Convert.ToBoolean(systemVal);
                    }

                    if (roleId == 0)
                    {
                        // INSERT new role
                        string insertQuery = @"
                            INSERT INTO dbo.Roles (Name, Name_En, Description, Description_En, IsSystem, IsActive, CreatedAt)
                            VALUES (@Name, @Name_En, @Description, @Description_En, 0, 1, GETUTCDATE());
                            SELECT SCOPE_IDENTITY();";

                        using var command = new SqlCommand(insertQuery, connection, transaction);
                        command.Parameters.AddWithValue("@Name", Name);
                        command.Parameters.AddWithValue("@Name_En", Name_En);
                        command.Parameters.AddWithValue("@Description", (object?)Description ?? DBNull.Value);
                        command.Parameters.AddWithValue("@Description_En", (object?)Description_En ?? DBNull.Value);

                        roleId = Convert.ToInt64(await command.ExecuteScalarAsync());
                        SuccessMessage = "تم إضافة الدور بنجاح.";
                    }
                    else
                    {
                        // UPDATE existing role
                        // If it's a system role, we don't change Name/Name_En to prevent breaking system mechanics
                        string updateQuery;
                        if (isSystem)
                        {
                            updateQuery = @"
                                UPDATE dbo.Roles 
                                SET Description = @Description, Description_En = @Description_En, UpdatedAt = GETUTCDATE()
                                WHERE Id = @Id";
                        }
                        else
                        {
                            updateQuery = @"
                                UPDATE dbo.Roles 
                                SET Name = @Name, Name_En = @Name_En, Description = @Description, Description_En = @Description_En, UpdatedAt = GETUTCDATE()
                                WHERE Id = @Id";
                        }

                        using var command = new SqlCommand(updateQuery, connection, transaction);
                        command.Parameters.AddWithValue("@Id", roleId);
                        command.Parameters.AddWithValue("@Description", (object?)Description ?? DBNull.Value);
                        command.Parameters.AddWithValue("@Description_En", (object?)Description_En ?? DBNull.Value);

                        if (!isSystem)
                        {
                            command.Parameters.AddWithValue("@Name", Name);
                            command.Parameters.AddWithValue("@Name_En", Name_En);
                        }

                        await command.ExecuteNonQueryAsync();
                        SuccessMessage = "تم تحديث الدور بنجاح.";
                    }

                    // For Super Administrator (Id = 1), we enforce having all permissions, and prevent altering them
                    if (roleId == 1)
                    {
                        // Always keep all permissions for Super Administrator
                        string deleteMapping = "DELETE FROM dbo.RolePermissions WHERE RoleId = 1";
                        using var deleteCmd = new SqlCommand(deleteMapping, connection, transaction);
                        await deleteCmd.ExecuteNonQueryAsync();

                        string insertAllMapping = @"
                            INSERT INTO dbo.RolePermissions (RoleId, PermissionId, CreatedAt)
                            SELECT 1, Id, GETUTCDATE() FROM dbo.Permissions";
                        using var insertAllCmd = new SqlCommand(insertAllMapping, connection, transaction);
                        await insertAllCmd.ExecuteNonQueryAsync();
                    }
                    else
                    {
                        // Update permissions mappings
                        // 1. Delete current mappings
                        string deleteMapping = "DELETE FROM dbo.RolePermissions WHERE RoleId = @RoleId";
                        using var deleteCmd = new SqlCommand(deleteMapping, connection, transaction);
                        deleteCmd.Parameters.AddWithValue("@RoleId", roleId);
                        await deleteCmd.ExecuteNonQueryAsync();

                        // 2. Insert new mappings
                        if (SelectedPermissionIds != null && SelectedPermissionIds.Count > 0)
                        {
                            string insertMapping = "INSERT INTO dbo.RolePermissions (RoleId, PermissionId, CreatedAt) VALUES (@RoleId, @PermissionId, GETUTCDATE())";
                            foreach (var permId in SelectedPermissionIds)
                            {
                                using var insertCmd = new SqlCommand(insertMapping, connection, transaction);
                                insertCmd.Parameters.AddWithValue("@RoleId", roleId);
                                insertCmd.Parameters.AddWithValue("@PermissionId", permId);
                                await insertCmd.ExecuteNonQueryAsync();
                            }
                        }
                    }

                    await transaction.CommitAsync();
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    ErrorMessage = $"حدث خطأ أثناء حفظ الدور وصلاحياته: {ex.Message}";
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

        public async Task<IActionResult> OnPostDeleteRoleAsync(long id)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return RedirectToPage("/Login");
            }

            if (!User.HasClaim("Permission", "Roles.Delete"))
            {
                TempData["ErrorMessage"] = "ليس لديك صلاحية كافية لحذف الأدوار.";
                return RedirectToPage("/Index");
            }

            if (id == 1)
            {
                ErrorMessage = "لا يمكن حذف دور مدير النظام العام (Super Administrator).";
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

                // Check if the role is a system role
                string checkQuery = "SELECT IsSystem FROM dbo.Roles WHERE Id = @Id";
                using var checkCmd = new SqlCommand(checkQuery, connection);
                checkCmd.Parameters.AddWithValue("@Id", id);
                var isSystemVal = await checkCmd.ExecuteScalarAsync();
                bool isSystem = isSystemVal != null && Convert.ToBoolean(isSystemVal);

                if (isSystem)
                {
                    ErrorMessage = "لا يمكن حذف الأدوار النظامية الأساسية.";
                    return RedirectToPage();
                }

                // Delete role (cascade deletes mappings in UserRoles and RolePermissions)
                string deleteQuery = "DELETE FROM dbo.Roles WHERE Id = @Id";
                using var deleteCmd = new SqlCommand(deleteQuery, connection);
                deleteCmd.Parameters.AddWithValue("@Id", id);
                await deleteCmd.ExecuteNonQueryAsync();

                SuccessMessage = "تم حذف الدور بنجاح.";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"حدث خطأ أثناء حذف الدور: {ex.Message}";
            }

            return RedirectToPage();
        }

        private async Task<IActionResult> OnGetReloadAsync()
        {
            string? connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (!string.IsNullOrEmpty(connectionString))
            {
                await LoadRolesAndPermissionsAsync(connectionString);
                if (Id > 0)
                {
                    await LoadRolePermissionsAsync(connectionString, Id);
                }
            }
            return Page();
        }

        public string GetFriendlyModuleName(string moduleName)
        {
            var (ar, en) = moduleName switch
            {
                "Content" => ("إدارة المحتوى والمقالات", "Content Management"),
                "Category" => ("إدارة الفئات والتصنيفات", "Category Management"),
                "ContentType" => ("أنواع المحتوى والحقول", "Content Types"),
                "Media" => ("مكتبة الوسائط والملفات", "Media Library"),
                "Settings" => ("الإعدادات العامة والموقع", "General Settings"),
                "Users" => ("إدارة حسابات المستخدمين", "User Management"),
                "Roles" => ("إدارة الأدوار", "Role Management"),
                "Permissions" => ("إدارة الصلاحيات", "Permission Management"),
                "Dashboard" => ("لوحة الإحصائيات والرئيسية", "Dashboard"),
                _ => (moduleName, moduleName)
            };

            return $"<span class=\"tp-only-ar\">{ar}</span><span class=\"tp-only-en\">{en}</span>";
        }

        private async Task LoadRolesAndPermissionsAsync(string connectionString)
        {
            RoleList.Clear();
            GroupedPermissions.Clear();

            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            // Load Roles
            string rolesQuery = "SELECT Id, Name, Name_En, Description, Description_En, IsSystem, IsActive FROM dbo.Roles ORDER BY IsSystem DESC, Name ASC";
            using (var rolesCmd = new SqlCommand(rolesQuery, connection))
            using (var reader = await rolesCmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    RoleList.Add(new RoleViewModel
                    {
                        Id = reader.GetInt64(0),
                        Name = reader.GetString(1),
                        Name_En = reader.GetString(2),
                        Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                        Description_En = reader.IsDBNull(4) ? null : reader.GetString(4),
                        IsSystem = reader.GetBoolean(5),
                        IsActive = reader.GetBoolean(6)
                    });
                }
            }

            // Load Permissions
            string permissionsQuery = "SELECT Id, Code, Name, Name_En, Description, Description_En FROM dbo.Permissions ORDER BY Code ASC";
            using (var permCmd = new SqlCommand(permissionsQuery, connection))
            using (var reader = await permCmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var permission = new PermissionViewModel
                    {
                        Id = reader.GetInt64(0),
                        Code = reader.GetString(1),
                        Name = reader.GetString(2),
                        Name_En = reader.GetString(3),
                        Description = reader.IsDBNull(4) ? null : reader.GetString(4),
                        Description_En = reader.IsDBNull(5) ? null : reader.GetString(5)
                    };

                    string module = permission.Code.Contains('.') ? permission.Code.Split('.')[0] : "General";
                    if (!GroupedPermissions.ContainsKey(module))
                    {
                        GroupedPermissions[module] = new List<PermissionViewModel>();
                    }
                    GroupedPermissions[module].Add(permission);
                }
            }
        }

        private async Task LoadRolePermissionsAsync(string connectionString, long roleId)
        {
            SelectedRolePermissionIds.Clear();

            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            string query = "SELECT PermissionId FROM dbo.RolePermissions WHERE RoleId = @RoleId";
            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@RoleId", roleId);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                SelectedRolePermissionIds.Add(reader.GetInt64(0));
            }
        }
    }

    public class RoleViewModel
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Name_En { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Description_En { get; set; }
        public bool IsSystem { get; set; }
        public bool IsActive { get; set; }
    }

    public class PermissionViewModel
    {
        public long Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Name_En { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Description_En { get; set; }
    }
}
