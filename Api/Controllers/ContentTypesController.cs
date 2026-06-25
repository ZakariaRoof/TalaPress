using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using TalaPress.Api.Helpers;
using TalaPress.Api.Security;

namespace TalaPress.Api.Controllers;

[ApiController]
[Route("api/v1/content-types")]
[Authorize(AuthenticationSchemes = PearlAuthenticationDefaults.AuthenticationScheme)]
public class ContentTypesController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public ContentTypesController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        string? connectionString = _configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return Problem("Database connection is not configured.");
        }

        var items = new List<object>();
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        const string query = @"
            SELECT Id, Name, Name_En, Description, Description_En, IconValue, IsSystem, IsActive
            FROM dbo.ContentTypes
            WHERE IsActive = 1
            ORDER BY IsSystem DESC, Name ASC";

        await using var command = new SqlCommand(query, connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new
            {
                id = reader.GetInt64(0),
                name = reader.GetString(1),
                nameEn = reader.IsDBNull(2) ? null : reader.GetString(2),
                description = reader.IsDBNull(3) ? null : reader.GetString(3),
                descriptionEn = reader.IsDBNull(4) ? null : reader.GetString(4),
                icon = reader.IsDBNull(5) ? null : reader.GetString(5),
                isSystem = reader.GetBoolean(6),
                isActive = reader.GetBoolean(7)
            });
        }

        return Ok(new { items });
    }

    /// <summary>
    /// Categories scoped for a content type (dedicated categories, or general fallback).
    /// </summary>
    [HttpGet("{id:long}/categories")]
    public async Task<IActionResult> GetCategoriesForContentType(long id, [FromQuery] bool activeOnly = true)
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

            var typeInfo = await CategoryApiHelper.GetContentTypeByIdAsync(connection, id, activeOnly);
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
