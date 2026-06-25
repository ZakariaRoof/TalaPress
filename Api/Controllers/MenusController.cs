using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using TalaPress.Api.Security;

namespace TalaPress.Api.Controllers;

[ApiController]
[Route("api/v1/menus")]
[Authorize(AuthenticationSchemes = PearlAuthenticationDefaults.AuthenticationScheme)]
public class MenusController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public MenusController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Lightweight index of navigation menus (no items).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool activeOnly = true)
    {
        string? connectionString = _configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return Problem("Database connection is not configured.");
        }

        var items = new List<object>();

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            string query = activeOnly
                ? @"
                    SELECT Id, Name, Name_En, Code, IsActive, CreatedAt, UpdatedAt
                    FROM dbo.Menus
                    WHERE IsActive = 1
                    ORDER BY Name ASC"
                : @"
                    SELECT Id, Name, Name_En, Code, IsActive, CreatedAt, UpdatedAt
                    FROM dbo.Menus
                    ORDER BY Name ASC";

            await using var command = new SqlCommand(query, connection);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                items.Add(new
                {
                    id = reader.GetInt64(0),
                    name = reader.GetString(1),
                    nameEn = ReadString(reader, 2),
                    code = reader.GetString(3),
                    isActive = reader.GetBoolean(4),
                    createdAt = reader.GetDateTime(5).ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    updatedAt = ReadDate(reader, 6)
                });
            }
        }
        catch (SqlException)
        {
            return Problem("Menu store is not ready.");
        }

        return Ok(new
        {
            data = items,
            meta = new
            {
                total = items.Count,
                activeOnly
            }
        });
    }

    /// <summary>
    /// Full hierarchical menu tree for a single menu identified by Code.
    /// </summary>
    [HttpGet("{code}")]
    public async Task<IActionResult> GetByCode(
        string code,
        [FromQuery] bool activeOnly = true,
        [FromQuery] bool resolveUrls = true)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return BadRequest(new { message = "Menu code is required." });
        }

        string? connectionString = _configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return Problem("Database connection is not configured.");
        }

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            string menuQuery = activeOnly
                ? @"
                    SELECT Id, Name, Name_En, Code, IsActive, CreatedAt, UpdatedAt
                    FROM dbo.Menus
                    WHERE Code = @Code AND IsActive = 1"
                : @"
                    SELECT Id, Name, Name_En, Code, IsActive, CreatedAt, UpdatedAt
                    FROM dbo.Menus
                    WHERE Code = @Code";

            long menuId;
            string menuName;
            string? menuNameEn;
            bool menuIsActive;
            string menuCreatedAt;
            string? menuUpdatedAt;

            await using (var menuCommand = new SqlCommand(menuQuery, connection))
            {
                menuCommand.Parameters.AddWithValue("@Code", code.Trim());
                await using var reader = await menuCommand.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                {
                    return NotFound(new { message = "Menu was not found." });
                }

                menuId = reader.GetInt64(0);
                menuName = reader.GetString(1);
                menuNameEn = ReadString(reader, 2);
                menuIsActive = reader.GetBoolean(4);
                menuCreatedAt = reader.GetDateTime(5).ToString("yyyy-MM-ddTHH:mm:ssZ");
                menuUpdatedAt = ReadDate(reader, 6);
            }

            var flatItems = await LoadMenuItemsAsync(connection, menuId, activeOnly);
            var tree = BuildMenuTree(flatItems, resolveUrls);

            return Ok(new
            {
                id = menuId,
                code = code.Trim(),
                name = menuName,
                nameEn = menuNameEn,
                isActive = menuIsActive,
                createdAt = menuCreatedAt,
                updatedAt = menuUpdatedAt,
                items = tree,
                meta = new
                {
                    activeOnly,
                    resolveUrls,
                    itemCount = flatItems.Count
                }
            });
        }
        catch (SqlException)
        {
            return Problem("Menu store is not ready.");
        }
    }

    private static async Task<List<MenuItemRow>> LoadMenuItemsAsync(SqlConnection connection, long menuId, bool activeOnly)
    {
        var rows = new List<MenuItemRow>();

        string query = activeOnly
            ? @"
                SELECT mi.Id, mi.ParentId, mi.Title, mi.Title_En, mi.Url,
                       mi.ContentTypeId, mi.CategoryId, mi.ContentId, mi.IconValue, mi.SortOrder, mi.IsActive,
                       ct.Name, ct.Name_En, ct.IconValue,
                       cat.Name, cat.Name_En, cat.Slug,
                       co.Title, co.Title_En, co.Slug, co.Status
                FROM dbo.MenuItems mi
                LEFT JOIN dbo.ContentTypes ct ON mi.ContentTypeId = ct.Id
                LEFT JOIN dbo.Categories cat ON mi.CategoryId = cat.Id
                LEFT JOIN dbo.Content co ON mi.ContentId = co.Id AND co.IsDeleted = 0
                WHERE mi.MenuId = @MenuId AND mi.IsActive = 1
                ORDER BY mi.SortOrder ASC, mi.Title ASC"
            : @"
                SELECT mi.Id, mi.ParentId, mi.Title, mi.Title_En, mi.Url,
                       mi.ContentTypeId, mi.CategoryId, mi.ContentId, mi.IconValue, mi.SortOrder, mi.IsActive,
                       ct.Name, ct.Name_En, ct.IconValue,
                       cat.Name, cat.Name_En, cat.Slug,
                       co.Title, co.Title_En, co.Slug, co.Status
                FROM dbo.MenuItems mi
                LEFT JOIN dbo.ContentTypes ct ON mi.ContentTypeId = ct.Id
                LEFT JOIN dbo.Categories cat ON mi.CategoryId = cat.Id
                LEFT JOIN dbo.Content co ON mi.ContentId = co.Id AND co.IsDeleted = 0
                WHERE mi.MenuId = @MenuId
                ORDER BY mi.SortOrder ASC, mi.Title ASC";

        await using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@MenuId", menuId);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(new MenuItemRow
            {
                Id = reader.GetInt64(0),
                ParentId = ReadNullableInt64(reader, 1),
                Title = reader.GetString(2),
                TitleEn = ReadString(reader, 3),
                Url = ReadString(reader, 4),
                ContentTypeId = ReadNullableInt64(reader, 5),
                CategoryId = ReadNullableInt64(reader, 6),
                ContentId = ReadNullableInt64(reader, 7),
                Icon = ReadString(reader, 8),
                SortOrder = reader.GetInt32(9),
                IsActive = reader.GetBoolean(10),
                ContentTypeName = ReadString(reader, 11),
                ContentTypeNameEn = ReadString(reader, 12),
                ContentTypeIcon = ReadString(reader, 13),
                CategoryName = ReadString(reader, 14),
                CategoryNameEn = ReadString(reader, 15),
                CategorySlug = ReadString(reader, 16),
                ContentTitle = ReadString(reader, 17),
                ContentTitleEn = ReadString(reader, 18),
                ContentSlug = ReadString(reader, 19),
                ContentStatus = ReadString(reader, 20)
            });
        }

        return rows;
    }

    private static List<object> BuildMenuTree(List<MenuItemRow> flatItems, bool resolveUrls, long? parentId = null)
    {
        return flatItems
            .Where(item => item.ParentId == parentId)
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Title)
            .Select(item => MapMenuItem(item, flatItems, resolveUrls))
            .ToList();
    }

    private static object MapMenuItem(MenuItemRow item, List<MenuItemRow> flatItems, bool resolveUrls)
    {
        string linkType = ResolveLinkType(item);
        string? resolvedUrl = resolveUrls ? ResolveItemUrl(item) : item.Url;

        return new
        {
            id = item.Id,
            title = item.Title,
            titleEn = item.TitleEn,
            url = resolvedUrl,
            rawUrl = item.Url,
            linkType,
            icon = item.Icon,
            sortOrder = item.SortOrder,
            isActive = item.IsActive,
            contentTypeId = item.ContentTypeId,
            categoryId = item.CategoryId,
            contentId = item.ContentId,
            contentType = item.ContentTypeId.HasValue
                ? new
                {
                    id = item.ContentTypeId.Value,
                    name = item.ContentTypeName,
                    nameEn = item.ContentTypeNameEn,
                    icon = item.ContentTypeIcon
                }
                : null,
            category = item.CategoryId.HasValue
                ? new
                {
                    id = item.CategoryId.Value,
                    name = item.CategoryName,
                    nameEn = item.CategoryNameEn,
                    slug = item.CategorySlug
                }
                : null,
            content = item.ContentId.HasValue
                ? new
                {
                    id = item.ContentId.Value,
                    title = item.ContentTitle,
                    titleEn = item.ContentTitleEn,
                    slug = item.ContentSlug,
                    status = item.ContentStatus
                }
                : null,
            children = BuildMenuTree(flatItems, resolveUrls, item.Id)
        };
    }

    private static string ResolveLinkType(MenuItemRow item)
    {
        if (item.ContentId.HasValue && item.ContentId.Value > 0)
        {
            return "content";
        }

        if (item.CategoryId.HasValue && item.CategoryId.Value > 0)
        {
            return "category";
        }

        if (item.ContentTypeId.HasValue && item.ContentTypeId.Value > 0)
        {
            return "contentType";
        }

        return "custom";
    }

    private static string? ResolveItemUrl(MenuItemRow item)
    {
        if (!string.IsNullOrWhiteSpace(item.Url))
        {
            return item.Url.Trim();
        }

        if (item.ContentId.HasValue &&
            string.Equals(item.ContentStatus, "Published", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(item.ContentSlug))
        {
            return NormalizePath(item.ContentSlug);
        }

        if (item.CategoryId.HasValue && !string.IsNullOrWhiteSpace(item.CategorySlug))
        {
            return NormalizePath(item.CategorySlug);
        }

        return null;
    }

    private static string NormalizePath(string slugOrPath)
    {
        string value = slugOrPath.Trim();
        if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith('/'))
        {
            return value;
        }

        return "/" + value;
    }

    private static string? ReadString(SqlDataReader reader, int index) =>
        reader.IsDBNull(index) ? null : reader.GetString(index);

    private static string? ReadDate(SqlDataReader reader, int index) =>
        reader.IsDBNull(index) ? null : reader.GetDateTime(index).ToString("yyyy-MM-ddTHH:mm:ssZ");

    private static long? ReadNullableInt64(SqlDataReader reader, int index) =>
        reader.IsDBNull(index) ? null : reader.GetInt64(index);

    private sealed class MenuItemRow
    {
        public long Id { get; set; }
        public long? ParentId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? TitleEn { get; set; }
        public string? Url { get; set; }
        public long? ContentTypeId { get; set; }
        public long? CategoryId { get; set; }
        public long? ContentId { get; set; }
        public string? Icon { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }
        public string? ContentTypeName { get; set; }
        public string? ContentTypeNameEn { get; set; }
        public string? ContentTypeIcon { get; set; }
        public string? CategoryName { get; set; }
        public string? CategoryNameEn { get; set; }
        public string? CategorySlug { get; set; }
        public string? ContentTitle { get; set; }
        public string? ContentTitleEn { get; set; }
        public string? ContentSlug { get; set; }
        public string? ContentStatus { get; set; }
    }
}
