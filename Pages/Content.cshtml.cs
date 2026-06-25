using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace TalaPress.Pages
{
    public class ContentModel : PageModel
    {
        private readonly IConfiguration _configuration;

        public ContentModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // Active Content Types for Card Selection and Filter
        public List<ContentTypeFilterDto> ContentTypesList { get; set; } = new();

        // Active Categories for Filter
        public List<CategoryFilterDto> CategoriesList { get; set; } = new();

        // Active Authors for Filter
        public List<AuthorFilterDto> AuthorsList { get; set; } = new();

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return RedirectToPage("/Login");
            }

            if (!User.HasClaim("Permission", "Content.View"))
            {
                TempData["ErrorMessage"] = "ليس لديك صلاحية كافية لعرض المحتوى.";
                return RedirectToPage("/Index");
            }

            string? connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                ErrorMessage = "جملة الاتصال بقاعدة البيانات غير مهيأة.";
                return Page();
            }

            await LoadMetadataAsync(connectionString);
            return Page();
        }

        // =========================================================================
        // Server-Side DataTables Handler
        // =========================================================================
        public async Task<IActionResult> OnPostServerSideDataAsync(
            int draw, int start, int length,
            [FromForm(Name = "search[value]")] string? searchValue,
            [FromForm(Name = "order[0][column]")] int? orderColumn,
            [FromForm(Name = "order[0][dir]")] string? orderDir,
            long? contentTypeId, long? categoryId, long? subCategoryId,
            string? status, long? authorId, string? dateFrom, string? dateTo)
        {
            if (User.Identity?.IsAuthenticated != true || !User.HasClaim("Permission", "Content.View"))
            {
                return new JsonResult(new { error = "Unauthorized access" });
            }

            string? connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                return new JsonResult(new { error = "Database connection string missing" });
            }

            int recordsTotal = 0;
            int recordsFiltered = 0;
            var dataList = new List<ContentRowDto>();

            try
            {
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                // 1. Get total records count (unfiltered, excluding deleted)
                string countTotalQuery = "SELECT COUNT(*) FROM dbo.Content WHERE IsDeleted = 0";
                using (var cmdTotal = new SqlCommand(countTotalQuery, connection))
                {
                    recordsTotal = Convert.ToInt32(await cmdTotal.ExecuteScalarAsync());
                }

                // 2. Build WHERE clauses dynamically
                var whereClauses = new List<string> { "c.IsDeleted = 0" };
                var sqlParams = new List<SqlParameter>();

                if (contentTypeId.HasValue && contentTypeId.Value > 0)
                {
                    whereClauses.Add("c.ContentTypeId = @ContentTypeId");
                    sqlParams.Add(new SqlParameter("@ContentTypeId", contentTypeId.Value));
                }

                if (categoryId.HasValue && categoryId.Value > 0)
                {
                    whereClauses.Add("c.CategoryId = @CategoryId");
                    sqlParams.Add(new SqlParameter("@CategoryId", categoryId.Value));
                }

                if (subCategoryId.HasValue && subCategoryId.Value > 0)
                {
                    whereClauses.Add("c.SubCategoryId = @SubCategoryId");
                    sqlParams.Add(new SqlParameter("@SubCategoryId", subCategoryId.Value));
                }

                if (!string.IsNullOrEmpty(status) && status != "ALL")
                {
                    whereClauses.Add("c.Status = @Status");
                    sqlParams.Add(new SqlParameter("@Status", status));
                }

                if (authorId.HasValue && authorId.Value > 0)
                {
                    whereClauses.Add("c.CreatedBy = @AuthorId");
                    sqlParams.Add(new SqlParameter("@AuthorId", authorId.Value));
                }

                if (!string.IsNullOrEmpty(dateFrom))
                {
                    whereClauses.Add("c.PublishDate >= @DateFrom");
                    sqlParams.Add(new SqlParameter("@DateFrom", DateTime.Parse(dateFrom)));
                }

                if (!string.IsNullOrEmpty(dateTo))
                {
                    whereClauses.Add("c.PublishDate <= @DateTo");
                    sqlParams.Add(new SqlParameter("@DateTo", DateTime.Parse(dateTo).AddDays(1).AddTicks(-1)));
                }

                if (!string.IsNullOrEmpty(searchValue))
                {
                    whereClauses.Add("(c.Title LIKE @Search OR c.Title_En LIKE @Search OR c.Summary LIKE @Search OR c.Summary_En LIKE @Search OR c.Content LIKE @Search OR c.Content_En LIKE @Search)");
                    sqlParams.Add(new SqlParameter("@Search", "%" + searchValue + "%"));
                }

                string whereString = string.Join(" AND ", whereClauses);

                // 3. Count filtered records
                string countFilteredQuery = $"SELECT COUNT(*) FROM dbo.Content c WHERE {whereString}";
                using (var cmdFiltered = new SqlCommand(countFilteredQuery, connection))
                {
                    foreach (var p in sqlParams)
                    {
                        cmdFiltered.Parameters.Add(new SqlParameter(p.ParameterName, p.Value));
                    }
                    recordsFiltered = Convert.ToInt32(await cmdFiltered.ExecuteScalarAsync());
                }

                // 4. Determine Sort Column
                string sortColumn = "c.CreatedAt";
                string sortDirection = "DESC";

                if (orderColumn.HasValue)
                {
                    sortColumn = orderColumn.Value switch
                    {
                        2 => "c.Title",
                        3 => "t.Name",
                        4 => "cat.Name",
                        5 => "sub.Name",
                        6 => "c.Status",
                        7 => "c.PublishDate",
                        8 => "u.FullName",
                        9 => "c.Hits",
                        10 => "c.CreatedAt",
                        _ => "c.CreatedAt"
                    };
                }

                if (!string.IsNullOrEmpty(orderDir))
                {
                    sortDirection = orderDir.ToUpper() == "ASC" ? "ASC" : "DESC";
                }

                // 5. Query data with offset paging
                string fetchClause = length > 0 
                    ? "OFFSET @Start ROWS FETCH NEXT @Length ROWS ONLY" 
                    : "OFFSET @Start ROWS";

                string selectQuery = $@"
                    SELECT 
                        c.Id, 
                        c.Title, 
                        c.Title_En, 
                        t.Name AS ContentTypeName, 
                        t.Name_En AS ContentTypeNameEn,
                        cat.Name AS CategoryName, 
                        cat.Name_En AS CategoryNameEn,
                        sub.Name AS SubCategoryName, 
                        sub.Name_En AS SubCategoryNameEn,
                        c.Status, 
                        c.PublishDate, 
                        u.FullName AS AuthorName, 
                        c.CreatedAt,
                        c.FeaturedImage,
                        c.Hits
                    FROM dbo.Content c
                    INNER JOIN dbo.ContentTypes t ON c.ContentTypeId = t.Id
                    LEFT JOIN dbo.Categories cat ON c.CategoryId = cat.Id
                    LEFT JOIN dbo.Categories sub ON c.SubCategoryId = sub.Id
                    INNER JOIN dbo.Users u ON c.CreatedBy = u.Id
                    WHERE {whereString}
                    ORDER BY {sortColumn} {sortDirection}
                    {fetchClause}";

                using (var cmdSelect = new SqlCommand(selectQuery, connection))
                {
                    foreach (var p in sqlParams)
                    {
                        cmdSelect.Parameters.Add(new SqlParameter(p.ParameterName, p.Value));
                    }
                    cmdSelect.Parameters.Add(new SqlParameter("@Start", start));
                    if (length > 0)
                    {
                        cmdSelect.Parameters.Add(new SqlParameter("@Length", length));
                    }

                    using var reader = await cmdSelect.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        dataList.Add(new ContentRowDto
                        {
                            Id = reader.GetInt64(0),
                            Title = reader.IsDBNull(1) ? null : reader.GetString(1),
                            Title_En = reader.IsDBNull(2) ? null : reader.GetString(2),
                            ContentTypeName = reader.GetString(3),
                            ContentTypeNameEn = reader.IsDBNull(4) ? null : reader.GetString(4),
                            CategoryName = reader.IsDBNull(5) ? null : reader.GetString(5),
                            CategoryNameEn = reader.IsDBNull(6) ? null : reader.GetString(6),
                            SubCategoryName = reader.IsDBNull(7) ? null : reader.GetString(7),
                            SubCategoryNameEn = reader.IsDBNull(8) ? null : reader.GetString(8),
                            Status = reader.GetString(9),
                            PublishDate = reader.IsDBNull(10) ? null : reader.GetDateTime(10),
                            AuthorName = reader.IsDBNull(11) ? "N/A" : reader.GetString(11),
                            CreatedAt = reader.GetDateTime(12),
                            FeaturedImage = reader.IsDBNull(13) ? null : reader.GetString(13),
                            Hits = reader.IsDBNull(14) ? 0 : reader.GetInt32(14)
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                return new JsonResult(new { error = ex.Message });
            }

            return new JsonResult(new
            {
                draw = draw,
                recordsTotal = recordsTotal,
                recordsFiltered = recordsFiltered,
                data = dataList
            });
        }

        // =========================================================================
        // Duplicate Action Page Handler
        // =========================================================================
        public async Task<IActionResult> OnPostDuplicateAsync(long id)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return RedirectToPage("/Login");
            }

            if (!User.HasClaim("Permission", "Content.Create"))
            {
                TempData["ErrorMessage"] = "ليس لديك صلاحية كافية لإنشاء وتكرار المحتوى.";
                return RedirectToPage("/Content");
            }

            string? connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                TempData["ErrorMessage"] = "جملة الاتصال بقاعدة البيانات غير مهيأة.";
                return RedirectToPage("/Content");
            }

            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            long currentUserId = userIdClaim != null ? long.Parse(userIdClaim.Value) : 0;

            try
            {
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                string selectQuery = @"
                    SELECT ContentTypeId, Title, Title_En, Slug, Summary, Summary_En, Content, Content_En, 
                           FeaturedImage, CategoryId, SubCategoryId, SeoTitle, SeoTitle_En, SeoDescription, 
                           SeoDescription_En, SeoKeywords, SeoKeywords_En, CanonicalUrl, CustomFieldsJson, Hits
                    FROM dbo.Content
                    WHERE Id = @Id AND IsDeleted = 0";

                long contentTypeId = 0;
                string? title = null, titleEn = null, slug = null, summary = null, summaryEn = null, content = null, contentEn = null;
                string? featuredImage = null, seoTitle = null, seoTitleEn = null, seoDesc = null, seoDescEn = null, seoKeywords = null, seoKeywordsEn = null, canonicalUrl = null;
                string? customFieldsJson = null;
                long? categoryId = null, subCategoryId = null;
                int hits = 0;

                using (var cmdSelect = new SqlCommand(selectQuery, connection))
                {
                    cmdSelect.Parameters.AddWithValue("@Id", id);
                    using var reader = await cmdSelect.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        contentTypeId = reader.GetInt64(0);
                        title = reader.IsDBNull(1) ? null : reader.GetString(1);
                        titleEn = reader.IsDBNull(2) ? null : reader.GetString(2);
                        slug = reader.IsDBNull(3) ? null : reader.GetString(3);
                        summary = reader.IsDBNull(4) ? null : reader.GetString(4);
                        summaryEn = reader.IsDBNull(5) ? null : reader.GetString(5);
                        content = reader.IsDBNull(6) ? null : reader.GetString(6);
                        contentEn = reader.IsDBNull(7) ? null : reader.GetString(7);
                        featuredImage = reader.IsDBNull(8) ? null : reader.GetString(8);
                        categoryId = reader.IsDBNull(9) ? null : reader.GetInt64(9);
                        subCategoryId = reader.IsDBNull(10) ? null : reader.GetInt64(10);
                        seoTitle = reader.IsDBNull(11) ? null : reader.GetString(11);
                        seoTitleEn = reader.IsDBNull(12) ? null : reader.GetString(12);
                        seoDesc = reader.IsDBNull(13) ? null : reader.GetString(13);
                        seoDescEn = reader.IsDBNull(14) ? null : reader.GetString(14);
                        seoKeywords = reader.IsDBNull(15) ? null : reader.GetString(15);
                        seoKeywordsEn = reader.IsDBNull(16) ? null : reader.GetString(16);
                        canonicalUrl = reader.IsDBNull(17) ? null : reader.GetString(17);
                        customFieldsJson = reader.IsDBNull(18) ? null : reader.GetString(18);
                        hits = reader.IsDBNull(19) ? 0 : reader.GetInt32(19);
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "تعذر العثور على المقال المختار لتكراره.";
                        return RedirectToPage("/Content");
                    }
                }

                // Add " - Copy" suffixes
                if (!string.IsNullOrEmpty(title)) title += " - Copy";
                if (!string.IsNullOrEmpty(titleEn)) titleEn += " - Copy";
                if (!string.IsNullOrEmpty(slug)) slug += "-copy";

                string insertQuery = @"
                    INSERT INTO dbo.Content 
                    (ContentTypeId, Title, Title_En, Slug, Summary, Summary_En, Content, Content_En, 
                     FeaturedImage, Status, PublishDate, CategoryId, SubCategoryId, SeoTitle, SeoTitle_En, 
                     SeoDescription, SeoDescription_En, SeoKeywords, SeoKeywords_En, CanonicalUrl, 
                     CustomFieldsJson, CreatedBy, CreatedAt, IsDeleted, Hits)
                     VALUES 
                    (@ContentTypeId, @Title, @Title_En, @Slug, @Summary, @Summary_En, @Content, @Content_En, 
                     @FeaturedImage, 'Draft', NULL, @CategoryId, @SubCategoryId, @SeoTitle, @SeoTitle_En, 
                     @SeoDescription, @SeoDescription_En, @SeoKeywords, @SeoKeywords_En, @CanonicalUrl, 
                     @CustomFieldsJson, @CreatedBy, GETUTCDATE(), 0, @Hits)";

                using (var cmdInsert = new SqlCommand(insertQuery, connection))
                {
                    cmdInsert.Parameters.AddWithValue("@ContentTypeId", contentTypeId);
                    cmdInsert.Parameters.AddWithValue("@Title", (object?)title ?? DBNull.Value);
                    cmdInsert.Parameters.AddWithValue("@Title_En", (object?)titleEn ?? DBNull.Value);
                    cmdInsert.Parameters.AddWithValue("@Slug", (object?)slug ?? DBNull.Value);
                    cmdInsert.Parameters.AddWithValue("@Summary", (object?)summary ?? DBNull.Value);
                    cmdInsert.Parameters.AddWithValue("@Summary_En", (object?)summaryEn ?? DBNull.Value);
                    cmdInsert.Parameters.AddWithValue("@Content", (object?)content ?? DBNull.Value);
                    cmdInsert.Parameters.AddWithValue("@Content_En", (object?)contentEn ?? DBNull.Value);
                    cmdInsert.Parameters.AddWithValue("@FeaturedImage", (object?)featuredImage ?? DBNull.Value);
                    cmdInsert.Parameters.AddWithValue("@CategoryId", (object?)categoryId ?? DBNull.Value);
                    cmdInsert.Parameters.AddWithValue("@SubCategoryId", (object?)subCategoryId ?? DBNull.Value);
                    cmdInsert.Parameters.AddWithValue("@SeoTitle", (object?)seoTitle ?? DBNull.Value);
                    cmdInsert.Parameters.AddWithValue("@SeoTitle_En", (object?)seoTitleEn ?? DBNull.Value);
                    cmdInsert.Parameters.AddWithValue("@SeoDescription", (object?)seoDesc ?? DBNull.Value);
                    cmdInsert.Parameters.AddWithValue("@SeoDescription_En", (object?)seoDescEn ?? DBNull.Value);
                    cmdInsert.Parameters.AddWithValue("@SeoKeywords", (object?)seoKeywords ?? DBNull.Value);
                    cmdInsert.Parameters.AddWithValue("@SeoKeywords_En", (object?)seoKeywordsEn ?? DBNull.Value);
                    cmdInsert.Parameters.AddWithValue("@CanonicalUrl", (object?)canonicalUrl ?? DBNull.Value);
                    cmdInsert.Parameters.AddWithValue("@CustomFieldsJson", (object?)customFieldsJson ?? DBNull.Value);
                    cmdInsert.Parameters.AddWithValue("@CreatedBy", currentUserId);
                    cmdInsert.Parameters.AddWithValue("@Hits", hits);

                    await cmdInsert.ExecuteNonQueryAsync();
                }

                TempData["SuccessMessage"] = "تم تكرار المقال كمسودة جديدة بنجاح.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"فشل تكرار المقال: {ex.Message}";
            }

            return RedirectToPage("/Content");
        }

        // =========================================================================
        // Soft Delete Page Handler
        // =========================================================================
        public async Task<IActionResult> OnPostDeleteAsync(long id)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return new JsonResult(new { success = false, message = "غير مسجل دخولك في النظام." });
            }

            if (!User.HasClaim("Permission", "Content.Delete"))
            {
                return new JsonResult(new { success = false, message = "ليس لديك صلاحية كافية لحذف المحتوى." });
            }

            string? connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                return new JsonResult(new { success = false, message = "جملة الاتصال بقاعدة البيانات غير مهيأة." });
            }

            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            long currentUserId = userIdClaim != null ? long.Parse(userIdClaim.Value) : 0;

            try
            {
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                string deleteQuery = @"
                    UPDATE dbo.Content
                    SET IsDeleted = 1,
                        DeletedAt = GETUTCDATE(),
                        DeletedBy = @DeletedBy
                    WHERE Id = @Id AND IsDeleted = 0";

                using var cmd = new SqlCommand(deleteQuery, connection);
                cmd.Parameters.AddWithValue("@Id", id);
                cmd.Parameters.AddWithValue("@DeletedBy", currentUserId);

                int affected = await cmd.ExecuteNonQueryAsync();
                if (affected > 0)
                {
                    return new JsonResult(new { success = true, message = "تم حذف المحتوى بنجاح." });
                }
                else
                {
                    return new JsonResult(new { success = false, message = "تعذر العثور على السجل المطلوب أو تم حذفه مسبقاً." });
                }
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = $"فشل عملية الحذف: {ex.Message}" });
            }
        }

        // =========================================================================
        // Bulk Soft Delete Page Handler
        // =========================================================================
        public async Task<IActionResult> OnPostBulkDeleteAsync(string ids)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return new JsonResult(new { success = false, message = "غير مسجل دخولك في النظام." });
            }

            if (!User.HasClaim("Permission", "Content.Delete"))
            {
                return new JsonResult(new { success = false, message = "ليس لديك صلاحية كافية لحذف المحتوى." });
            }

            if (string.IsNullOrEmpty(ids))
            {
                return new JsonResult(new { success = false, message = "لم يتم تحديد أي سجلات لحذفها." });
            }

            string? connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                return new JsonResult(new { success = false, message = "جملة الاتصال بقاعدة البيانات غير مهيأة." });
            }

            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            long currentUserId = userIdClaim != null ? long.Parse(userIdClaim.Value) : 0;

            try
            {
                var idList = ids.Split(',').Select(long.Parse).ToList();
                if (!idList.Any())
                {
                    return new JsonResult(new { success = false, message = "لم يتم تحديد أي سجلات لحذفها." });
                }

                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                string idPlaceholder = string.Join(",", idList.Select((id, index) => $"@Id{index}"));
                string deleteQuery = $@"
                    UPDATE dbo.Content
                    SET IsDeleted = 1,
                        DeletedAt = GETUTCDATE(),
                        DeletedBy = @DeletedBy
                    WHERE Id IN ({idPlaceholder}) AND IsDeleted = 0";

                using var cmd = new SqlCommand(deleteQuery, connection);
                cmd.Parameters.AddWithValue("@DeletedBy", currentUserId);
                for (int i = 0; i < idList.Count; i++)
                {
                    cmd.Parameters.AddWithValue($"@Id{i}", idList[i]);
                }

                int affected = await cmd.ExecuteNonQueryAsync();
                if (affected > 0)
                {
                    return new JsonResult(new { success = true, message = $"تم حذف {affected} من السجلات بنجاح." });
                }
                else
                {
                    return new JsonResult(new { success = false, message = "لم يتم حذف أي سجلات. قد تكون قد حُذفت بالفعل." });
                }
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = $"فشل عملية الحذف الجماعي: {ex.Message}" });
            }
        }

        // =========================================================================
        // Load Metadata Helpers
        // =========================================================================
        private async Task LoadMetadataAsync(string connectionString)
        {
            ContentTypesList.Clear();
            CategoriesList.Clear();
            AuthorsList.Clear();

            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            // 1. Load active content types
            string selectTypes = "SELECT Id, Name, Name_En, Description, Description_En, IconValue FROM dbo.ContentTypes WHERE IsActive = 1 ORDER BY Name ASC";
            using (var cmdTypes = new SqlCommand(selectTypes, connection))
            using (var reader = await cmdTypes.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    ContentTypesList.Add(new ContentTypeFilterDto
                    {
                        Id = reader.GetInt64(0),
                        Name = reader.GetString(1),
                        Name_En = reader.IsDBNull(2) ? null : reader.GetString(2),
                        Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                        Description_En = reader.IsDBNull(4) ? null : reader.GetString(4),
                        IconValue = reader.IsDBNull(5) ? null : reader.GetString(5)
                    });
                }
            }

            // 2. Load active categories
            string selectCategories = "SELECT Id, Name, Name_En, ParentId FROM dbo.Categories WHERE IsActive = 1 ORDER BY SortOrder ASC, Name ASC";
            using (var cmdCats = new SqlCommand(selectCategories, connection))
            using (var reader = await cmdCats.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    CategoriesList.Add(new CategoryFilterDto
                    {
                        Id = reader.GetInt64(0),
                        Name = reader.GetString(1),
                        Name_En = reader.IsDBNull(2) ? null : reader.GetString(2),
                        ParentId = reader.IsDBNull(3) ? null : reader.GetInt64(3)
                    });
                }
            }

            // 3. Load active authors
            string selectUsers = "SELECT Id, FullName, Username FROM dbo.Users WHERE IsActive = 1 ORDER BY FullName ASC, Username ASC";
            using (var cmdUsers = new SqlCommand(selectUsers, connection))
            using (var reader = await cmdUsers.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    AuthorsList.Add(new AuthorFilterDto
                    {
                        Id = reader.GetInt64(0),
                        FullName = reader.IsDBNull(1) ? reader.GetString(2) : reader.GetString(1),
                        Username = reader.GetString(2)
                    });
                }
            }
        }
    }

    // =========================================================================
    // DTO Definitions
    // =========================================================================
    public class ContentTypeFilterDto
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Name_En { get; set; }
        public string? Description { get; set; }
        public string? Description_En { get; set; }
        public string? IconValue { get; set; }
    }

    public class CategoryFilterDto
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Name_En { get; set; }
        public long? ParentId { get; set; }
        public long? ContentTypeId { get; set; }
    }

    public class AuthorFilterDto
    {
        public long Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
    }

    public class ContentRowDto
    {
        public long Id { get; set; }
        public string? Title { get; set; }
        public string? Title_En { get; set; }
        public string ContentTypeName { get; set; } = string.Empty;
        public string? ContentTypeNameEn { get; set; }
        public string? CategoryName { get; set; }
        public string? CategoryNameEn { get; set; }
        public string? SubCategoryName { get; set; }
        public string? SubCategoryNameEn { get; set; }
        public string Status { get; set; } = "Draft";
        public DateTime? PublishDate { get; set; }
        public string AuthorName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string? FeaturedImage { get; set; }
        public int Hits { get; set; }
    }
}
