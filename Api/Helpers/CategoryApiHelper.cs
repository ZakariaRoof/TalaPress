using Microsoft.Data.SqlClient;

namespace TalaPress.Api.Helpers;

internal static class CategoryApiHelper
{
    internal sealed class ContentTypeInfo
    {
        public long Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string? NameEn { get; init; }
        public bool IsActive { get; init; }
    }

    internal static async Task EnsureContentTypeIdColumnAsync(SqlConnection connection)
    {
        const string query = @"
            IF COL_LENGTH('dbo.Categories', 'ContentTypeId') IS NULL
            BEGIN
                ALTER TABLE dbo.Categories ADD ContentTypeId BIGINT NULL;
                CREATE INDEX IX_Categories_ContentTypeId ON dbo.Categories(ContentTypeId, ParentId, SortOrder);
            END";

        await using var command = new SqlCommand(query, connection);
        await command.ExecuteNonQueryAsync();
    }

    internal static async Task<ContentTypeInfo?> GetContentTypeByIdAsync(SqlConnection connection, long id, bool activeOnly)
    {
        string query = activeOnly
            ? @"SELECT Id, Name, Name_En, IsActive FROM dbo.ContentTypes WHERE Id = @Id AND IsActive = 1"
            : @"SELECT Id, Name, Name_En, IsActive FROM dbo.ContentTypes WHERE Id = @Id";

        await using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@Id", id);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new ContentTypeInfo
        {
            Id = reader.GetInt64(0),
            Name = reader.GetString(1),
            NameEn = ReadString(reader, 2),
            IsActive = reader.GetBoolean(3)
        };
    }

    internal static async Task<ContentTypeInfo?> GetContentTypeByNameAsync(SqlConnection connection, string name, bool activeOnly)
    {
        string query = activeOnly
            ? @"SELECT Id, Name, Name_En, IsActive FROM dbo.ContentTypes WHERE (Name = @Name OR Name_En = @Name) AND IsActive = 1"
            : @"SELECT Id, Name, Name_En, IsActive FROM dbo.ContentTypes WHERE Name = @Name OR Name_En = @Name";

        await using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@Name", name.Trim());
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new ContentTypeInfo
        {
            Id = reader.GetInt64(0),
            Name = reader.GetString(1),
            NameEn = ReadString(reader, 2),
            IsActive = reader.GetBoolean(3)
        };
    }

    /// <summary>
    /// Mirrors ContentEdit scope: dedicated categories for the type, otherwise general (NULL) categories.
    /// </summary>
    internal static async Task<(long? ScopeContentTypeId, string Scope)> ResolveCategoryScopeAsync(SqlConnection connection, long contentTypeId)
    {
        const string dedicatedQuery = @"
            SELECT COUNT(*)
            FROM dbo.Categories
            WHERE IsActive = 1 AND ContentTypeId = @ContentTypeId";

        await using var command = new SqlCommand(dedicatedQuery, connection);
        command.Parameters.AddWithValue("@ContentTypeId", contentTypeId);
        int dedicatedCount = Convert.ToInt32(await command.ExecuteScalarAsync());

        if (dedicatedCount > 0)
        {
            return (contentTypeId, "dedicated");
        }

        return (null, "general");
    }

    internal static async Task<List<object>> LoadCategoriesAsync(SqlConnection connection, long? scopeContentTypeId, bool activeOnly)
    {
        var items = new List<object>();

        string whereActive = activeOnly ? "IsActive = 1" : "1 = 1";
        string scopeFilter = scopeContentTypeId.HasValue
            ? "ContentTypeId = @ScopeContentTypeId"
            : "ContentTypeId IS NULL";

        string query = $@"
            SELECT Id, Name, Name_En, Slug, ParentId, ContentTypeId, IconValue, Image, SortOrder, IsActive
            FROM dbo.Categories
            WHERE {whereActive} AND {scopeFilter}
            ORDER BY ParentId, SortOrder, Name";

        await using var command = new SqlCommand(query, connection);
        if (scopeContentTypeId.HasValue)
        {
            command.Parameters.AddWithValue("@ScopeContentTypeId", scopeContentTypeId.Value);
        }

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(MapCategory(reader));
        }

        return items;
    }

    internal static async Task<List<object>> LoadAllCategoriesAsync(SqlConnection connection, bool activeOnly)
    {
        var items = new List<object>();
        string whereActive = activeOnly ? "WHERE IsActive = 1" : string.Empty;

        string query = $@"
            SELECT Id, Name, Name_En, Slug, ParentId, ContentTypeId, IconValue, Image, SortOrder, IsActive
            FROM dbo.Categories
            {whereActive}
            ORDER BY ParentId, SortOrder, Name";

        await using var command = new SqlCommand(query, connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(MapCategory(reader));
        }

        return items;
    }

    private static object MapCategory(SqlDataReader reader)
    {
        return new
        {
            id = reader.GetInt64(0),
            name = reader.GetString(1),
            nameEn = ReadString(reader, 2),
            slug = ReadString(reader, 3),
            parentId = ReadNullableInt64(reader, 4),
            contentTypeId = ReadNullableInt64(reader, 5),
            icon = ReadString(reader, 6),
            image = ReadString(reader, 7),
            sortOrder = reader.GetInt32(8),
            isActive = reader.GetBoolean(9)
        };
    }

    private static string? ReadString(SqlDataReader reader, int index) =>
        reader.IsDBNull(index) ? null : reader.GetString(index);

    private static long? ReadNullableInt64(SqlDataReader reader, int index) =>
        reader.IsDBNull(index) ? null : reader.GetInt64(index);
}
