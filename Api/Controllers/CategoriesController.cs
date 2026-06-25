using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using TalaPress.Api.Helpers;
using TalaPress.Api.Security;

namespace TalaPress.Api.Controllers;

[ApiController]
[Route("api/v1/categories")]
[Authorize(AuthenticationSchemes = PearlAuthenticationDefaults.AuthenticationScheme)]
public class CategoriesController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public CategoriesController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// List categories. Use contentTypeId or contentType to apply the same scope rules as the admin panel.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] long? contentTypeId = null,
        [FromQuery] string? contentType = null,
        [FromQuery] bool activeOnly = true)
    {
        string? connectionString = _configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return Problem("Database connection is not configured.");
        }

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            await CategoryApiHelper.EnsureContentTypeIdColumnAsync(connection);

            if (!contentTypeId.HasValue && !string.IsNullOrWhiteSpace(contentType))
            {
                var resolvedType = await CategoryApiHelper.GetContentTypeByNameAsync(connection, contentType, activeOnly);
                if (resolvedType == null)
                {
                    return NotFound(new { message = "Content type was not found." });
                }

                contentTypeId = resolvedType.Id;
            }

            if (!contentTypeId.HasValue)
            {
                var allItems = await CategoryApiHelper.LoadAllCategoriesAsync(connection, activeOnly);
                return Ok(new
                {
                    items = allItems,
                    meta = new
                    {
                        total = allItems.Count,
                        activeOnly,
                        scope = "all"
                    }
                });
            }

            var typeInfo = await CategoryApiHelper.GetContentTypeByIdAsync(connection, contentTypeId.Value, activeOnly);
            if (typeInfo == null)
            {
                return NotFound(new { message = "Content type was not found." });
            }

            var (scopeContentTypeId, scope) = await CategoryApiHelper.ResolveCategoryScopeAsync(connection, typeInfo.Id);
            var items = await CategoryApiHelper.LoadCategoriesAsync(connection, scopeContentTypeId, activeOnly);

            return Ok(new
            {
                contentType = new
                {
                    id = typeInfo.Id,
                    name = typeInfo.Name,
                    nameEn = typeInfo.NameEn
                },
                scope,
                items,
                meta = new
                {
                    total = items.Count,
                    activeOnly,
                    scopeContentTypeId
                }
            });
        }
        catch (SqlException)
        {
            return Problem("Category store is not ready.");
        }
    }
}
