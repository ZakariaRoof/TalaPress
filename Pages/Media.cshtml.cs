using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace TalaPress.Pages
{
    public class MediaModel : PageModel
    {
        private readonly IConfiguration _configuration;

        public MediaModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public string CurrentFolder { get; set; } = string.Empty;
        public string SearchQuery { get; set; } = string.Empty;

        public List<BreadcrumbItem> Breadcrumbs { get; set; } = new();
        public List<FolderItem> Directories { get; set; } = new();
        public List<FileItem> Files { get; set; } = new();

        public int MaxUploadSizeMB { get; set; } = 20;
        public string AllowedFileExtensions { get; set; } = "jpg,jpeg,png,gif,webp,pdf,doc,docx,xls,xlsx,ppt,pptx,txt,zip,mp3,mp4";
        public string AllFoldersJson { get; set; } = "[]";

        public async Task<IActionResult> OnGetAsync(string? folder, string? search)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return RedirectToPage("/Login");
            }

            if (!User.HasClaim("Permission", "Media.View"))
            {
                TempData["ErrorMessage"] = "ليس لديك صلاحية كافية لعرض مكتبة الوسائط.";
                return RedirectToPage("/Index");
            }

            string baseUploadsDir = GetBaseUploadsDirectory();
            string? targetDir = ResolveAndVerifyPath(baseUploadsDir, folder, out string verifiedRelPath);
            if (targetDir == null)
            {
                TempData["ErrorMessage"] = "محاولة وصول غير مصرح بها خارج مجلد الرفع.";
                return RedirectToPage();
            }

            CurrentFolder = verifiedRelPath;
            SearchQuery = search ?? string.Empty;

            // Load site settings for file constraints
            await LoadUploadSettingsAsync();

            // Load all subfolders recursively
            LoadAllFolders(baseUploadsDir);

            // Build breadcrumbs
            BuildBreadcrumbs(verifiedRelPath);

            try
            {
                if (!string.IsNullOrEmpty(SearchQuery))
                {
                    // Search recursively in base directory
                    SearchFilesRecursively(baseUploadsDir, SearchQuery);
                }
                else
                {
                    // Read normal folders and files in current folder
                    var dirPaths = Directory.GetDirectories(targetDir);
                    foreach (var dir in dirPaths)
                    {
                        var dirName = Path.GetFileName(dir);
                        var relPath = Path.GetRelativePath(baseUploadsDir, dir).Replace('\\', '/');
                        Directories.Add(new FolderItem
                        {
                            Name = dirName,
                            RelativePath = relPath,
                            ItemCount = Directory.GetFileSystemEntries(dir).Length
                        });
                    }

                    var filePaths = Directory.GetFiles(targetDir);
                    foreach (var file in filePaths)
                    {
                        var fileInfo = new FileInfo(file);
                        var webRelPath = "/uploads/" + Path.GetRelativePath(baseUploadsDir, file).Replace('\\', '/');
                        Files.Add(new FileItem
                        {
                            Name = fileInfo.Name,
                            RelativePath = webRelPath,
                            PhysicalPath = Path.GetRelativePath(baseUploadsDir, file).Replace('\\', '/'),
                            Size = fileInfo.Length,
                            Extension = fileInfo.Extension.ToLowerInvariant(),
                            CreatedAt = fileInfo.CreationTime
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"حدث خطأ أثناء قراءة المجلد: {ex.Message}";
            }

            return Page();
        }

        public IActionResult OnPostCreateFolder(string? folder, string folderName)
        {
            if (User.Identity?.IsAuthenticated != true) return RedirectToPage("/Login");
            if (!User.HasClaim("Permission", "Media.Upload"))
            {
                TempData["ErrorMessage"] = "ليس لديك صلاحية لإنشاء مجلدات.";
                return RedirectToPage();
            }

            if (string.IsNullOrWhiteSpace(folderName))
            {
                TempData["ErrorMessage"] = "اسم المجلد مطلوب.";
                return RedirectToPage(new { folder });
            }

            // Clean folder name to prevent traversals or weird chars
            folderName = CleanFolderName(folderName);
            if (string.IsNullOrEmpty(folderName))
            {
                TempData["ErrorMessage"] = "اسم المجلد غير صالح.";
                return RedirectToPage(new { folder });
            }

            // Validation: Do not allow manual year folder creation for 2026-2035 at uploads root
            if (string.IsNullOrEmpty(folder) || folder == "/" || folder == ".")
            {
                if (int.TryParse(folderName, out int yearVal) && yearVal >= 2026 && yearVal <= 2035)
                {
                    TempData["ErrorMessage"] = "غير مسموح بإنشاء مجلدات الأرشيف السنوية يدوياً.";
                    return RedirectToPage(new { folder });
                }
            }

            string baseUploadsDir = GetBaseUploadsDirectory();
            string? targetDir = ResolveAndVerifyPath(baseUploadsDir, folder, out string verifiedRelPath);
            if (targetDir == null)
            {
                TempData["ErrorMessage"] = "محاولة وصول غير مصرح بها.";
                return RedirectToPage();
            }

            try
            {
                string newFolderPath = Path.Combine(targetDir, folderName);
                if (Directory.Exists(newFolderPath))
                {
                    TempData["ErrorMessage"] = "المجلد موجود بالفعل.";
                }
                else
                {
                    Directory.CreateDirectory(newFolderPath);
                    TempData["SuccessMessage"] = $"تم إنشاء المجلد '{folderName}' بنجاح.";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"فشل إنشاء المجلد: {ex.Message}";
            }

            return RedirectToPage(new { folder = verifiedRelPath });
        }

        public IActionResult OnPostDelete(string? folder, string itemPath, string type)
        {
            bool isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";
            
            if (User.Identity?.IsAuthenticated != true) 
            {
                if (isAjax) return new JsonResult(new { success = false, message = "جلسة العمل انتهت، يرجى تسجيل الدخول." });
                return RedirectToPage("/Login");
            }
            if (!User.HasClaim("Permission", "Media.Delete"))
            {
                string msg = "ليس لديك صلاحية لحذف الملفات أو المجلدات.";
                if (isAjax) return new JsonResult(new { success = false, message = msg });
                TempData["ErrorMessage"] = msg;
                return RedirectToPage();
            }

            if (string.IsNullOrEmpty(itemPath))
            {
                string msg = "مسار العنصر المطلوب حذفه غير محدد.";
                if (isAjax) return new JsonResult(new { success = false, message = msg });
                TempData["ErrorMessage"] = msg;
                return RedirectToPage(new { folder });
            }

            string baseUploadsDir = GetBaseUploadsDirectory();
            // Verify path of item to delete is within uploads directory
            string fullItemPath = Path.GetFullPath(Path.Combine(baseUploadsDir, itemPath));
            if (!fullItemPath.StartsWith(baseUploadsDir, StringComparison.OrdinalIgnoreCase))
            {
                string msg = "محاولة حذف غير مصرح بها خارج مجلد الرفع.";
                if (isAjax) return new JsonResult(new { success = false, message = msg });
                TempData["ErrorMessage"] = msg;
                return RedirectToPage();
            }

            // Validation: Reject deleting annual archive folders (2026-2035) at uploads root
            string itemRel = Path.GetRelativePath(baseUploadsDir, fullItemPath).Replace('\\', '/');

            // Prevent deleting img folder or its contents
            if (itemRel.Equals("img", StringComparison.OrdinalIgnoreCase) || itemRel.StartsWith("img/", StringComparison.OrdinalIgnoreCase))
            {
                string msg = "غير مسموح بحذف مجلد الإعدادات (img) أو محتوياته من مكتبة الوسائط.";
                if (isAjax) return new JsonResult(new { success = false, message = msg });
                TempData["ErrorMessage"] = msg;
                return RedirectToPage(new { folder });
            }

            if (type == "folder" && !itemRel.Contains('/'))
            {
                if (int.TryParse(itemRel, out int yearVal) && yearVal >= 2026 && yearVal <= 2035)
                {
                    string msg = "غير مسموح بحذف مجلدات الأرشيف السنوية.";
                    if (isAjax) return new JsonResult(new { success = false, message = msg });
                    TempData["ErrorMessage"] = msg;
                    return RedirectToPage(new { folder });
                }
            }

            string? successMsg = null;
            string? errorMsg = null;

            try
            {
                if (type == "folder")
                {
                    if (Directory.Exists(fullItemPath))
                    {
                        Directory.Delete(fullItemPath, true);
                        successMsg = "تم حذف المجلد وكافة محتوياته بنجاح.";
                    }
                    else
                    {
                        errorMsg = "المجلد غير موجود.";
                    }
                }
                else
                {
                    if (System.IO.File.Exists(fullItemPath))
                    {
                        System.IO.File.Delete(fullItemPath);
                        successMsg = "تم حذف الملف بنجاح.";
                    }
                    else
                    {
                        errorMsg = "الملف غير موجود.";
                    }
                }
            }
            catch (Exception ex)
            {
                errorMsg = $"حدث خطأ أثناء الحذف: {ex.Message}";
            }

            if (isAjax)
            {
                if (errorMsg != null)
                {
                    return new JsonResult(new { success = false, message = errorMsg });
                }
                return new JsonResult(new { success = true, message = successMsg });
            }

            if (errorMsg != null) TempData["ErrorMessage"] = errorMsg;
            if (successMsg != null) TempData["SuccessMessage"] = successMsg;

            return RedirectToPage(new { folder });
        }

        public async Task<IActionResult> OnPostUploadAsync(string? folder, IFormFile file)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return new JsonResult(new { success = false, message = "جلسة العمل انتهت، يرجى تسجيل الدخول." });
            }
            if (!User.HasClaim("Permission", "Media.Upload"))
            {
                return new JsonResult(new { success = false, message = "ليس لديك صلاحية لرفع الملفات." });
            }

            if (file == null || file.Length == 0)
            {
                return new JsonResult(new { success = false, message = "لم يتم اختيار أي ملف للرفع." });
            }

            string baseUploadsDir = GetBaseUploadsDirectory();
            if (string.IsNullOrEmpty(folder))
            {
                folder = Request.Form["folder"];
            }
            string? targetDir = ResolveAndVerifyPath(baseUploadsDir, folder, out string verifiedRelPath);
            if (targetDir == null)
            {
                return new JsonResult(new { success = false, message = "مسار رفع غير صالح." });
            }

            // Load configurations
            await LoadUploadSettingsAsync();

            // Validate extension
            string ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (string.IsNullOrEmpty(ext))
            {
                return new JsonResult(new { success = false, message = "نوع الملف غير صالح (لا يحتوي على امتداد)." });
            }

            var allowedExts = AllowedFileExtensions
                .Split(new[] { ',', '|', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(e => e.Trim().ToLowerInvariant().Replace(".", ""))
                .ToList();

            string extWithoutDot = ext.Replace(".", "");
            // Double check safe extensions (hard blocking typical scripts/executables)
            string[] blockedExts = { "exe", "dll", "bat", "cmd", "sh", "php", "asp", "aspx", "cshtml", "jsp", "py", "pl", "rb", "js", "config", "json" };
            if (blockedExts.Contains(extWithoutDot) || !allowedExts.Contains(extWithoutDot))
            {
                return new JsonResult(new { success = false, message = $"نوع الملف غير مسموح برسمه. الامتدادات المدعومة: {AllowedFileExtensions}" });
            }

            // Validate size
            long maxBytes = (long)MaxUploadSizeMB * 1024 * 1024;
            if (file.Length > maxBytes)
            {
                return new JsonResult(new { success = false, message = $"حجم الملف يتجاوز الحد الأقصى المسموح به وهو {MaxUploadSizeMB} ميجابايت." });
            }

            try
            {
                // Sanitize filename
                string cleanName = Path.GetFileNameWithoutExtension(file.FileName);
                cleanName = Regex.Replace(cleanName, @"[^a-zA-Z0-9_\-\u0600-\u06FF]", ""); // Allow letters, numbers, hyphens, and Arabic characters
                if (string.IsNullOrWhiteSpace(cleanName))
                {
                    cleanName = "uploaded_file";
                }

                // If file exists, generate unique name
                string finalName = cleanName + ext;
                string destPath = Path.Combine(targetDir, finalName);
                int count = 1;
                while (System.IO.File.Exists(destPath))
                {
                    finalName = $"{cleanName}_{count}{ext}";
                    destPath = Path.Combine(targetDir, finalName);
                    count++;
                }

                using (var stream = new FileStream(destPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                string relativePath = "/uploads/" + Path.GetRelativePath(baseUploadsDir, destPath).Replace('\\', '/');
                return new JsonResult(new { success = true, filePath = relativePath, fileName = finalName });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = $"حدث خطأ أثناء حفظ الملف: {ex.Message}" });
            }
        }

        public IActionResult OnPostRename([FromForm] string? folder, [FromForm] string itemPath, [FromForm] string newName, [FromForm] string type)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return new JsonResult(new { success = false, message = "جلسة العمل انتهت، يرجى تسجيل الدخول." });
            }
            if (!User.HasClaim("Permission", "Media.Upload"))
            {
                return new JsonResult(new { success = false, message = "ليس لديك صلاحية لتعديل الأسماء." });
            }

            if (string.IsNullOrWhiteSpace(itemPath) || string.IsNullOrWhiteSpace(newName))
            {
                return new JsonResult(new { success = false, message = "البيانات المرسلة غير مكتملة." });
            }

            string baseUploadsDir = GetBaseUploadsDirectory();
            string fullItemPath = Path.GetFullPath(Path.Combine(baseUploadsDir, itemPath));

            if (!fullItemPath.StartsWith(baseUploadsDir, StringComparison.OrdinalIgnoreCase))
            {
                return new JsonResult(new { success = false, message = "محاولة وصول غير مصرح بها خارج مجلد الرفع." });
            }

            // Validation: Reject renaming an existing archive folder (2026-2035) at uploads root
            string itemRel = Path.GetRelativePath(baseUploadsDir, fullItemPath).Replace('\\', '/');

            // Prevent renaming img folder or its contents
            if (itemRel.Equals("img", StringComparison.OrdinalIgnoreCase) || itemRel.StartsWith("img/", StringComparison.OrdinalIgnoreCase))
            {
                return new JsonResult(new { success = false, message = "غير مسموح بتعديل اسم مجلد الإعدادات (img) أو محتوياته." });
            }

            if (type == "folder" && !itemRel.Contains('/'))
            {
                if (int.TryParse(itemRel, out int yearVal) && yearVal >= 2026 && yearVal <= 2035)
                {
                    return new JsonResult(new { success = false, message = "غير مسموح بتعديل أسماء مجلدات الأرشيف السنوية." });
                }
            }

            try
            {
                string parentDir = Path.GetDirectoryName(fullItemPath) ?? baseUploadsDir;
                string finalNewName = newName.Trim();

                if (type == "folder")
                {
                    finalNewName = CleanFolderName(finalNewName);
                    if (string.IsNullOrEmpty(finalNewName))
                    {
                        return new JsonResult(new { success = false, message = "اسم المجلد الجديد غير صالح." });
                    }

                    // Validation: Reject renaming folder to a year folder name (2026-2035) at uploads root
                    string relativeParent = Path.GetRelativePath(baseUploadsDir, parentDir).Replace('\\', '/');
                    if (relativeParent == "." || string.IsNullOrEmpty(relativeParent) || relativeParent == "/")
                    {
                        if (int.TryParse(finalNewName, out int newYearVal) && newYearVal >= 2026 && newYearVal <= 2035)
                        {
                            return new JsonResult(new { success = false, message = "غير مسموح بإنشاء مجلدات أرشيف سنوية عبر إعادة التسمية." });
                        }
                    }

                    string newFullPath = Path.Combine(parentDir, finalNewName);
                    if (Directory.Exists(newFullPath) || System.IO.File.Exists(newFullPath))
                    {
                        return new JsonResult(new { success = false, message = "يوجد مجلد أو ملف آخر بنفس الاسم في هذا المسار." });
                    }
                    Directory.Move(fullItemPath, newFullPath);
                }
                else
                {
                    string ext = Path.GetExtension(fullItemPath).ToLowerInvariant();
                    string cleanName = Path.GetFileNameWithoutExtension(finalNewName);
                    cleanName = Regex.Replace(cleanName, @"[^a-zA-Z0-9_\-\u0600-\u06FF]", "");
                    if (string.IsNullOrEmpty(cleanName))
                    {
                        return new JsonResult(new { success = false, message = "اسم الملف الجديد غير صالح." });
                    }
                    finalNewName = cleanName + ext;
                    string newFullPath = Path.Combine(parentDir, finalNewName);
                    if (System.IO.File.Exists(newFullPath) || Directory.Exists(newFullPath))
                    {
                        return new JsonResult(new { success = false, message = "يوجد ملف أو مجلد آخر بنفس الاسم في هذا المسار." });
                    }
                    System.IO.File.Move(fullItemPath, newFullPath);
                }

                return new JsonResult(new { success = true });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = $"فشل تغيير الاسم: {ex.Message}" });
            }
        }

        public IActionResult OnPostMove([FromForm] string? folder, [FromForm] string itemPath, [FromForm] string targetFolder, [FromForm] string type)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return new JsonResult(new { success = false, message = "جلسة العمل انتهت، يرجى تسجيل الدخول." });
            }
            if (!User.HasClaim("Permission", "Media.Upload"))
            {
                return new JsonResult(new { success = false, message = "ليس لديك صلاحية لنقل الملفات." });
            }

            if (string.IsNullOrWhiteSpace(itemPath))
            {
                return new JsonResult(new { success = false, message = "مسار العنصر المراد نقله غير محدد." });
            }

            string targetRel = (targetFolder == "/" || string.IsNullOrEmpty(targetFolder)) ? "" : targetFolder.TrimStart('/');
            string baseUploadsDir = GetBaseUploadsDirectory();
            string fullItemPath = Path.GetFullPath(Path.Combine(baseUploadsDir, itemPath));
            string fullTargetDir = Path.GetFullPath(Path.Combine(baseUploadsDir, targetRel));

            if (!fullItemPath.StartsWith(baseUploadsDir, StringComparison.OrdinalIgnoreCase) || 
                !fullTargetDir.StartsWith(baseUploadsDir, StringComparison.OrdinalIgnoreCase))
            {
                return new JsonResult(new { success = false, message = "محاولة وصول غير مصرح بها." });
            }

            // Validation: Reject moving a top-level year folder (2026-2035)
            string itemRel = Path.GetRelativePath(baseUploadsDir, fullItemPath).Replace('\\', '/');

            // Prevent moving img folder or its contents
            if (itemRel.Equals("img", StringComparison.OrdinalIgnoreCase) || itemRel.StartsWith("img/", StringComparison.OrdinalIgnoreCase))
            {
                return new JsonResult(new { success = false, message = "غير مسموح بنقل مجلد الإعدادات (img) أو محتوياته." });
            }

            if (type == "folder" && !itemRel.Contains('/'))
            {
                if (int.TryParse(itemRel, out int yearVal) && yearVal >= 2026 && yearVal <= 2035)
                {
                    return new JsonResult(new { success = false, message = "غير مسموح بنقل مجلدات الأرشيف السنوية." });
                }
            }

            try
            {
                string itemName = Path.GetFileName(fullItemPath);
                string newFullPath = Path.Combine(fullTargetDir, itemName);

                if (type == "folder")
                {
                    if (!Directory.Exists(fullItemPath))
                    {
                        return new JsonResult(new { success = false, message = "المجلد الأصلي غير موجود." });
                    }
                    if (fullTargetDir.StartsWith(fullItemPath, StringComparison.OrdinalIgnoreCase))
                    {
                        return new JsonResult(new { success = false, message = "لا يمكن نقل المجلد إلى داخل نفسه أو داخل أحد مجلداته الفرعية." });
                    }
                    if (Directory.Exists(newFullPath))
                    {
                        return new JsonResult(new { success = false, message = "يوجد مجلد آخر بنفس الاسم في الوجهة المحددة." });
                    }
                    Directory.Move(fullItemPath, newFullPath);
                }
                else
                {
                    if (!System.IO.File.Exists(fullItemPath))
                    {
                        return new JsonResult(new { success = false, message = "الملف الأصلي غير موجود." });
                    }
                    if (System.IO.File.Exists(newFullPath))
                    {
                        return new JsonResult(new { success = false, message = "يوجد ملف آخر بنفس الاسم في الوجهة المحددة." });
                    }
                    System.IO.File.Move(fullItemPath, newFullPath);
                }

                return new JsonResult(new { success = true });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = $"فشل نقل العنصر: {ex.Message}" });
            }
        }

        private void LoadAllFolders(string baseDir)
        {
            var folders = new List<string> { "/" };
            try
            {
                var options = new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    MaxRecursionDepth = 10,
                    IgnoreInaccessible = true
                };
                var dirs = Directory.GetDirectories(baseDir, "*", options);
                foreach (var dir in dirs)
                {
                    var rel = Path.GetRelativePath(baseDir, dir).Replace('\\', '/');
                    folders.Add("/" + rel);
                }
            }
            catch
            {
                // Ignore
            }
            AllFoldersJson = System.Text.Json.JsonSerializer.Serialize(folders);
        }

        #region Helpers

        private string GetBaseUploadsDirectory()
        {
            return Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
        }

        private string? ResolveAndVerifyPath(string baseDir, string? folder, out string verifiedRelPath)
        {
            verifiedRelPath = string.Empty;
            if (string.IsNullOrEmpty(folder))
            {
                return baseDir;
            }

            // Clean folder to avoid leading slashes and resolve path
            string combined = Path.Combine(baseDir, folder);
            string fullPath = Path.GetFullPath(combined);

            // Security check
            if (!fullPath.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            // Create target folder if it doesn't exist
            if (!Directory.Exists(fullPath))
            {
                Directory.CreateDirectory(fullPath);
            }

            verifiedRelPath = Path.GetRelativePath(baseDir, fullPath).Replace('\\', '/');
            if (verifiedRelPath == "." || verifiedRelPath == "./")
            {
                verifiedRelPath = string.Empty;
            }

            return fullPath;
        }

        private string CleanFolderName(string name)
        {
            // Remove invalid folder name characters and directory traversals
            name = Regex.Replace(name, @"[\\/:*?""<>|.]", "");
            return name.Trim();
        }

        private void BuildBreadcrumbs(string relativePath)
        {
            Breadcrumbs.Add(new BreadcrumbItem { Name = "الرئيسية (uploads)", Path = "" });
            if (string.IsNullOrEmpty(relativePath)) return;

            string[] parts = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            string accumulated = "";
            foreach (var part in parts)
            {
                accumulated = string.IsNullOrEmpty(accumulated) ? part : $"{accumulated}/{part}";
                Breadcrumbs.Add(new BreadcrumbItem { Name = part, Path = accumulated });
            }
        }

        private void SearchFilesRecursively(string baseDir, string query)
        {
            var allFiles = Directory.GetFiles(baseDir, "*.*", SearchOption.AllDirectories);
            foreach (var file in allFiles)
            {
                var fileInfo = new FileInfo(file);
                if (fileInfo.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    var webRelPath = "/uploads/" + Path.GetRelativePath(baseDir, file).Replace('\\', '/');
                    Files.Add(new FileItem
                    {
                        Name = fileInfo.Name,
                        RelativePath = webRelPath,
                        PhysicalPath = Path.GetRelativePath(baseDir, file).Replace('\\', '/'),
                        Size = fileInfo.Length,
                        Extension = fileInfo.Extension.ToLowerInvariant(),
                        CreatedAt = fileInfo.CreationTime
                    });
                }
            }
        }

        private async Task LoadUploadSettingsAsync()
        {
            string? connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString)) return;

            try
            {
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                const string query = "SELECT MaxUploadSizeMB, AllowedFileExtensions FROM dbo.Settings WHERE Id = 1";
                using var command = new SqlCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    MaxUploadSizeMB = reader.GetInt32(0);
                    AllowedFileExtensions = reader.IsDBNull(1) ? AllowedFileExtensions : reader.GetString(1);
                }
            }
            catch
            {
                // Fallback to default values defined as properties
            }
        }

        #endregion
    }

    public class BreadcrumbItem
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
    }

    public class FolderItem
    {
        public string Name { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty;
        public int ItemCount { get; set; }
    }

    public class FileItem
    {
        public string Name { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty; // Relative path for web (starts with /uploads/)
        public string PhysicalPath { get; set; } = string.Empty; // Relative path from uploads/ directory on disk
        public long Size { get; set; }
        public string Extension { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
