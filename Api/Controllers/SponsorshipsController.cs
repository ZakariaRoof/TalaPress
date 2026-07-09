using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using TalaPress.Api.Security;

namespace TalaPress.Api.Controllers;

[ApiController]
[Route("api/v1")]
[Authorize(AuthenticationSchemes = PearlAuthenticationDefaults.AuthenticationScheme)]
public sealed class SponsorshipsController : ControllerBase
{
    private const int QatarCountryId = 542;
    private const int MaxFetchedSponsorships = 10000;
    private const int OnlineEmployeeId = 1100683;
    private const string QatarCharityExportPathBaseUrl = "https://portal.qcharity.net/ExportPath/";

    private readonly IConfiguration _configuration;
    private readonly ILogger<SponsorshipsController> _logger;

    public SponsorshipsController(IConfiguration configuration, ILogger<SponsorshipsController> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    [HttpGet("projects")]
    public async Task<IActionResult> GetProjects(
        [FromQuery] int languageId = 1,
        [FromQuery] int typeId = 2,
        [FromQuery] int countryId = QatarCountryId,
        [FromQuery] int projectTypeId = 0,
        [FromQuery] int projectSubTypeId = 0,
        [FromQuery] string? searchQuery = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 12,
        [FromQuery] string? sortBy = "newest",
        CancellationToken cancellationToken = default)
    {
        if (typeId != 2)
        {
            return BadRequest(new { message = "Only projects are available from this endpoint in TalaPress." });
        }

        if (countryId != QatarCountryId)
        {
            return BadRequest(new { message = "Alakraboon projects are limited to Qatar countryId 542." });
        }

        var result = await BuildProjectsResponseAsync(
            NormalizeLanguageId(languageId),
            NormalizePositive(projectTypeId),
            NormalizePositive(projectSubTypeId),
            searchQuery,
            pageNumber,
            pageSize,
            sortBy,
            cancellationToken);

        if (result is null)
        {
            return Problem("Projects data source is not configured.");
        }

        return Ok(new
        {
            items = result.Items.Select(ToProjectDonationItem).ToList(),
            data = result.Items.Select(ToProjectDonationItem).ToList(),
            tabs = result.Tabs,
            pagination = result.Pagination,
            meta = result.Meta,
            filters = result.Filters,
            ui = new
            {
                mainTab = new { key = "projects", label = NormalizeLanguageId(languageId) == 1 ? "المشاريع" : "Projects", typeId = 2, donationsTypeId = 0 },
                cardFilterParameter = "projectTypeId",
                sourceFilterParameter = "typeId",
                subFilters = Array.Empty<object>()
            }
        });
    }

    [HttpGet("sponsorships")]
    public async Task<IActionResult> GetSponsorships(
        [FromQuery] string? language = null,
        [FromQuery] int? languageId = null,
        [FromQuery] int? categoryTypeId = null,
        [FromQuery] int? sponsorshipCategoryId = null,
        [FromQuery] string? searchQuery = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 12,
        [FromQuery] string? sortBy = "newest",
        [FromQuery] bool availableOnly = true,
        [FromQuery] bool mostWaiting = false,
        [FromQuery] bool birthday = false,
        CancellationToken cancellationToken = default)
    {
        var effectiveLanguageId = ResolveLanguageId(language, languageId);
        var selectedCategoryId = NormalizePositive(categoryTypeId) ?? NormalizePositive(sponsorshipCategoryId);
        var result = await BuildSponsorshipsResponseAsync(
            effectiveLanguageId,
            selectedCategoryId,
            searchQuery,
            pageNumber,
            pageSize,
            sortBy,
            mostWaiting,
            birthday,
            availableOnly,
            cancellationToken);

        return result is null ? Problem("Sponsorships data source is not configured.") : Ok(result);
    }

    [HttpGet("donation-items")]
    public async Task<IActionResult> GetDonationItems(
        [FromQuery] int languageId = 1,
        [FromQuery] int typeId = 3,
        [FromQuery] int donationsTypeId = 0,
        [FromQuery] int countryId = QatarCountryId,
        [FromQuery] int categoryId = 0,
        [FromQuery] int sponsorshipCategoryId = 0,
        [FromQuery] int needTypeId = 0,
        [FromQuery] string? searchQuery = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 12,
        [FromQuery] string? sortBy = "newest",
        [FromQuery(Name = "mostwaiting")] bool mostWaiting = true,
        [FromQuery] bool birthday = false,
        [FromQuery] bool? enableSadaka = null,
        [FromQuery] bool? enableZakaa = null,
        CancellationToken cancellationToken = default)
    {
        if (typeId != 1 && typeId != 3)
        {
            return BadRequest(new { message = "Only donation and sponsorship items are available from this endpoint in TalaPress." });
        }

        if (countryId != QatarCountryId)
        {
            return BadRequest(new { message = "Alakraboon donation items are limited to Qatar countryId 542." });
        }

        if (typeId == 1)
        {
            var donationResult = await BuildDonationItemsResponseAsync(
                NormalizeLanguageId(languageId),
                NormalizeDonationTypeId(donationsTypeId),
                NormalizePositive(categoryId),
                NormalizePositive(needTypeId),
                searchQuery,
                pageNumber,
                pageSize,
                sortBy,
                mostWaiting,
                enableSadaka,
                enableZakaa,
                cancellationToken);

            if (donationResult is null)
            {
                return Problem("Donation items data source is not configured.");
            }

            return Ok(new
            {
                items = donationResult.Items.Select(ToGeneralDonationItem).ToList(),
                data = donationResult.Items.Select(ToGeneralDonationItem).ToList(),
                tabs = donationResult.Tabs,
                pagination = donationResult.Pagination,
                meta = donationResult.Meta,
                filters = donationResult.Filters,
                ui = donationResult.Ui
            });
        }

        var result = await BuildSponsorshipsResponseAsync(
            NormalizeLanguageId(languageId),
            NormalizePositive(sponsorshipCategoryId),
            searchQuery,
            pageNumber,
            pageSize,
            sortBy,
            mostWaiting,
            birthday,
            availableOnly: true,
            cancellationToken);

        if (result is null)
        {
            return Problem("Sponsorships data source is not configured.");
        }

        var donationItems = result.Items.Select(ToDonationItem).ToList();

        return Ok(new
        {
            items = donationItems,
            data = donationItems,
            tabs = result.Tabs,
            pagination = result.Pagination,
            meta = result.Meta,
            filters = new
            {
                languageId = NormalizeLanguageId(languageId),
                typeId = 3,
                sponsorshipCategoryId = NormalizePositive(sponsorshipCategoryId),
                searchQuery = NormalizeSearch(searchQuery),
                mostWaiting,
                sortBy = NormalizeSort(sortBy)
            },
            ui = new
            {
                mainTab = new { key = "sponsorships", label = NormalizeLanguageId(languageId) == 1 ? "الكفالات" : "Sponsorships", typeId = 3, donationsTypeId = 0 },
                cardFilterParameter = "sponsorshipCategoryId",
                sourceFilterParameter = "typeId",
                subFilters = Array.Empty<object>()
            }
        });
    }

    private async Task<SponsorshipsResponse?> BuildSponsorshipsResponseAsync(
        int languageId,
        int? selectedCategoryId,
        string? searchQuery,
        int pageNumber,
        int pageSize,
        string? sortBy,
        bool mostWaiting,
        bool birthday,
        bool availableOnly,
        CancellationToken cancellationToken)
    {
        var connectionString = _configuration.GetConnectionString("SponsorshipsConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        pageNumber = Math.Max(pageNumber, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        languageId = NormalizeLanguageId(languageId);
        var normalizedSearch = NormalizeSearch(searchQuery);
        var normalizedSort = NormalizeSort(sortBy);

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var categories = await LoadCategoriesAsync(connection, languageId, cancellationToken);
        var allItems = await LoadSponsorshipItemsAsync(
            connection,
            languageId,
            normalizedSearch,
            mostWaiting,
            birthday,
            cancellationToken);

        var filteredItems = selectedCategoryId.HasValue
            ? allItems.Where(item => item.CategoryTypeId == selectedCategoryId.Value).ToList()
            : allItems;

        filteredItems = ApplySort(filteredItems, normalizedSort);

        var totalItems = filteredItems.Count;
        var totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);
        var pageItems = filteredItems
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var tabs = BuildTabs(categories, allItems, selectedCategoryId, languageId);
        var resultsLabel = selectedCategoryId.HasValue
            ? $"نتائج التبويب: {totalItems} حالة"
            : $"{totalItems} حالة متاحة";

        return new SponsorshipsResponse
        {
            Tabs = tabs,
            Filters = new
            {
                selectedTab = selectedCategoryId,
                searchQuery = normalizedSearch,
                availableOnly,
                sortBy = normalizedSort
            },
            Items = pageItems,
            Pagination = new SponsorshipPagination
            {
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalItems = totalItems,
                TotalPages = totalPages,
                HasPreviousPage = pageNumber > 1,
                HasNextPage = totalPages > 0 && pageNumber < totalPages
            },
            Meta = new
            {
                language = languageId == 1 ? "ar" : "en",
                languageId,
                typeId = 3,
                categoryTypeId = selectedCategoryId,
                sponsorshipCategoryId = selectedCategoryId,
                searchQuery = normalizedSearch,
                pageNumber,
                pageSize,
                sortBy = normalizedSort,
                mostWaiting,
                availableOnly,
                totalCount = totalItems,
                totalPages,
                totalAvailableCount = allItems.Count,
                availableCategoryIds = tabs.Where(tab => !tab.IsAll && tab.Count > 0 && tab.Id.HasValue).Select(tab => tab.Id!.Value).ToList(),
                resultsLabel
            }
        };
    }

    private async Task<ProjectsResponse?> BuildProjectsResponseAsync(
        int languageId,
        int? selectedProjectTypeId,
        int? selectedProjectSubTypeId,
        string? searchQuery,
        int pageNumber,
        int pageSize,
        string? sortBy,
        CancellationToken cancellationToken)
    {
        var connectionString = _configuration.GetConnectionString("SponsorshipsConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        pageNumber = Math.Max(pageNumber, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        languageId = NormalizeLanguageId(languageId);
        var normalizedSearch = NormalizeSearch(searchQuery);
        var normalizedSort = NormalizeSort(sortBy);

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var allItems = await LoadProjectItemsAsync(
            connection,
            languageId,
            selectedProjectTypeId,
            selectedProjectSubTypeId,
            normalizedSearch,
            pageIndex: 0,
            pageSize: MaxFetchedSponsorships,
            cancellationToken);

        var filteredItems = ApplyProjectSort(allItems, normalizedSort);
        var totalItems = filteredItems.Count;
        var totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);
        var pageItems = filteredItems
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();
        var tabs = BuildProjectTabs(allItems, selectedProjectTypeId, languageId);

        return new ProjectsResponse
        {
            Tabs = tabs,
            Filters = new
            {
                languageId,
                typeId = 2,
                countryId = QatarCountryId,
                projectTypeId = selectedProjectTypeId,
                projectSubTypeId = selectedProjectSubTypeId,
                searchQuery = normalizedSearch,
                sortBy = normalizedSort
            },
            Items = pageItems,
            Pagination = new SponsorshipPagination
            {
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalItems = totalItems,
                TotalPages = totalPages,
                HasPreviousPage = pageNumber > 1,
                HasNextPage = totalPages > 0 && pageNumber < totalPages
            },
            Meta = new
            {
                language = languageId == 1 ? "ar" : "en",
                languageId,
                typeId = 2,
                countryId = QatarCountryId,
                projectTypeId = selectedProjectTypeId,
                projectSubTypeId = selectedProjectSubTypeId,
                searchQuery = normalizedSearch,
                pageNumber,
                pageSize,
                sortBy = normalizedSort,
                totalCount = totalItems,
                totalPages,
                totalAvailableCount = allItems.Count,
                availableCategoryIds = tabs.Where(tab => !tab.IsAll && tab.Count > 0 && tab.Id.HasValue).Select(tab => tab.Id!.Value).ToList(),
                resultsLabel = selectedProjectTypeId.HasValue
                    ? $"نتائج الفئة: {totalItems} مشروع"
                    : $"{totalItems} مشروع متاح"
            }
        };
    }

    private async Task<DonationItemsResponse?> BuildDonationItemsResponseAsync(
        int languageId,
        int donationsTypeId,
        int? selectedCategoryId,
        int? selectedNeedTypeId,
        string? searchQuery,
        int pageNumber,
        int pageSize,
        string? sortBy,
        bool mostWaiting,
        bool? enableSadaka,
        bool? enableZakaa,
        CancellationToken cancellationToken)
    {
        var connectionString = _configuration.GetConnectionString("SponsorshipsConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        pageNumber = Math.Max(pageNumber, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        languageId = NormalizeLanguageId(languageId);
        donationsTypeId = NormalizeDonationTypeId(donationsTypeId);
        var normalizedSearch = NormalizeSearch(searchQuery);
        var normalizedSort = NormalizeSort(sortBy);

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var allItems = donationsTypeId == 3
            ? await LoadAssistanceCaseItemsAsync(connection, languageId, normalizedSearch, pageSize: MaxFetchedSponsorships, cancellationToken)
            : await LoadContributionProjectItemsAsync(connection, languageId, normalizedSearch, enableSadaka, enableZakaa, pageSize: MaxFetchedSponsorships, cancellationToken);

        var filteredItems = donationsTypeId == 3 && selectedNeedTypeId.HasValue
            ? allItems.Where(item => item.NeedTypeId == selectedNeedTypeId.Value).ToList()
            : donationsTypeId == 4 && selectedCategoryId.HasValue
                ? allItems.Where(item => item.MainAccountTypeId == selectedCategoryId.Value).ToList()
                : allItems;

        filteredItems = ApplyGeneralDonationSort(filteredItems, normalizedSort, mostWaiting);

        var totalItems = filteredItems.Count;
        var totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);
        var pageItems = filteredItems
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();
        var tabs = donationsTypeId == 3
            ? BuildAssistanceTabs(allItems, selectedNeedTypeId, languageId)
            : BuildContributionTabs(allItems, selectedCategoryId, languageId);
        var filterParameter = donationsTypeId == 3 ? "needTypeId" : "categoryId";
        var tabKey = donationsTypeId == 3 ? "assistance" : (mostWaiting ? "complete-together" : "contribution-types");
        var tabLabel = donationsTypeId == 3
            ? (languageId == 1 ? "الحالات الإنسانية" : "Assistance Cases")
            : mostWaiting
                ? (languageId == 1 ? "لنُكملها معاً" : "Complete Together")
                : (languageId == 1 ? "أنواع المساهمات" : "Contribution Types");
        var selectedFilterId = donationsTypeId == 3 ? selectedNeedTypeId : selectedCategoryId;

        return new DonationItemsResponse
        {
            Tabs = tabs,
            Filters = new
            {
                languageId,
                typeId = 1,
                countryId = QatarCountryId,
                donationsTypeId,
                categoryId = selectedCategoryId,
                needTypeId = selectedNeedTypeId,
                searchQuery = normalizedSearch,
                mostWaiting,
                sortBy = normalizedSort,
                enableSadaka,
                enableZakaa
            },
            Ui = new
            {
                mainTab = new { key = tabKey, label = tabLabel, typeId = 1, donationsTypeId },
                cardFilterParameter = filterParameter,
                sourceFilterParameter = "donationsTypeId",
                subFilters = Array.Empty<object>()
            },
            Items = pageItems,
            Pagination = new SponsorshipPagination
            {
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalItems = totalItems,
                TotalPages = totalPages,
                HasPreviousPage = pageNumber > 1,
                HasNextPage = totalPages > 0 && pageNumber < totalPages
            },
            Meta = new
            {
                language = languageId == 1 ? "ar" : "en",
                languageId,
                typeId = 1,
                countryId = QatarCountryId,
                donationsTypeId,
                selectedFilterId,
                searchQuery = normalizedSearch,
                pageNumber,
                pageSize,
                sortBy = normalizedSort,
                mostWaiting,
                totalCount = totalItems,
                totalPages,
                totalAvailableCount = allItems.Count,
                availableCategoryIds = tabs.Where(tab => !tab.IsAll && tab.Count > 0 && tab.Id.HasValue).Select(tab => tab.Id!.Value).ToList(),
                resultsLabel = selectedFilterId.HasValue
                    ? $"نتائج التبويب: {totalItems} حالة"
                    : $"{totalItems} حالة متاحة"
            }
        };
    }

    private static async Task<List<SponsorshipCategoryRow>> LoadCategoriesAsync(SqlConnection connection, int languageId, CancellationToken cancellationToken)
    {
        const string query = @"
DECLARE @LangId INT = @LanguageId;

SELECT
    0 AS SponoshipsCategoryId,
    CASE
        WHEN @LangId = 1 THEN N'نوع الكفالة'
        ELSE N'Sponsorship Type'
    END AS SponoshipsCategoryName
UNION ALL
SELECT
    person_category_id AS SponoshipsCategoryId,
    CASE
        WHEN @LangId = 1 THEN person_category_description
        ELSE ISNULL(NULLIF(person_category_description_Eng, ''), person_category_description)
    END AS SponoshipsCategoryName
FROM QCGPINTEGCOREDB.dbo.person_categories
WHERE person_category_id NOT IN (4, 6, 7)
ORDER BY SponoshipsCategoryId;";

        var categories = new List<SponsorshipCategoryRow>();
        await using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@LanguageId", languageId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var id = ReadInt(reader, "SponoshipsCategoryId") ?? 0;
            categories.Add(new SponsorshipCategoryRow(id, ReadString(reader, "SponoshipsCategoryName") ?? id.ToString()));
        }

        return categories;
    }

    private async Task<List<SponsorshipItemDto>> LoadSponsorshipItemsAsync(
        SqlConnection connection,
        int languageId,
        string? searchQuery,
        bool mostWaiting,
        bool birthday,
        CancellationToken cancellationToken)
    {
        var items = new List<SponsorshipItemDto>();

        await using var command = new SqlCommand("GetNonSponsorships", connection)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 30
        };

        command.Parameters.AddWithValue("@CountryId", QatarCountryId);
        command.Parameters.AddWithValue("@MainAccountTypeId", DBNull.Value);
        command.Parameters.AddWithValue("@SponoshipsCategoryId", DBNull.Value);
        command.Parameters.AddWithValue("@searchText", string.IsNullOrWhiteSpace(searchQuery) ? string.Empty : searchQuery);
        command.Parameters.AddWithValue("@allowAmountFilter", 0);
        command.Parameters.AddWithValue("@minAmount", 0);
        command.Parameters.AddWithValue("@maxAmount", 0);
        command.Parameters.AddWithValue("@day", 0);
        command.Parameters.AddWithValue("@month", 0);
        command.Parameters.AddWithValue("@year", 0);
        command.Parameters.AddWithValue("@Age", 0);
        command.Parameters.AddWithValue("@PageIndex", 0);
        command.Parameters.AddWithValue("@PageSize", MaxFetchedSponsorships);
        command.Parameters.AddWithValue("@mostWaiting", mostWaiting);
        command.Parameters.AddWithValue("@sortOrder", DBNull.Value);
        command.Parameters.AddWithValue("@birthday", birthday);

        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                items.Add(MapSponsorshipItem(reader, languageId));
            }
        }
        catch (SqlException ex)
        {
            _logger.LogError(ex, "Failed to fetch QC non-sponsored sponsorships.");
            throw;
        }

        return items;
    }

    private async Task<List<ProjectItemDto>> LoadProjectItemsAsync(
        SqlConnection connection,
        int languageId,
        int? projectTypeId,
        int? projectSubTypeId,
        string? searchQuery,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var items = new List<ProjectItemDto>();

        await using var command = new SqlCommand("ProjectsAvailableGet", connection)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 60
        };

        command.Parameters.AddWithValue("@MainAccountTypeId", DBNull.Value);
        command.Parameters.AddWithValue("@CountryId", QatarCountryId);
        command.Parameters.AddWithValue("@EmployeeId", OnlineEmployeeId);
        command.Parameters.AddWithValue("@minAmount", 0);
        command.Parameters.AddWithValue("@maxAmount", 0);
        command.Parameters.AddWithValue("@searchText", string.IsNullOrWhiteSpace(searchQuery) ? string.Empty : searchQuery.Trim());
        command.Parameters.AddWithValue("@ProjectTypeId", projectTypeId.HasValue ? projectTypeId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@ProjectSubTypeId", projectSubTypeId.HasValue ? projectSubTypeId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@PageIndex", Math.Max(pageIndex, 0));
        command.Parameters.AddWithValue("@PageSize", Math.Clamp(pageSize, 1, MaxFetchedSponsorships));
        command.Parameters.AddWithValue("@LanguageId", languageId);

        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var item = MapProjectItem(reader, languageId);
                if (item.CountryId == QatarCountryId || !item.CountryId.HasValue)
                {
                    items.Add(item);
                }
            }
        }
        catch (SqlException ex)
        {
            _logger.LogError(ex, "Failed to fetch QC available projects.");
            throw;
        }

        return items;
    }

    private async Task<List<DonationItemDto>> LoadContributionProjectItemsAsync(
        SqlConnection connection,
        int languageId,
        string? searchQuery,
        bool? enableSadaka,
        bool? enableZakaa,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var items = new List<DonationItemDto>();

        await using var command = new SqlCommand("ContributionProjectsGetV2", connection)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 60
        };

        command.Parameters.AddWithValue("@MainAccountTypeId", DBNull.Value);
        command.Parameters.AddWithValue("@CountryId", QatarCountryId);
        command.Parameters.AddWithValue("@EmployeeId", OnlineEmployeeId);
        command.Parameters.AddWithValue("@minAmount", 0);
        command.Parameters.AddWithValue("@maxAmount", 0);
        command.Parameters.AddWithValue("@searchText", string.IsNullOrWhiteSpace(searchQuery) ? string.Empty : searchQuery.Trim());
        command.Parameters.AddWithValue("@ProjectTypeId", DBNull.Value);
        command.Parameters.AddWithValue("@ProjectSubTypeId", DBNull.Value);
        command.Parameters.AddWithValue("@GetClosedCauses", 0);
        command.Parameters.AddWithValue("@LanguageId", languageId);
        command.Parameters.AddWithValue("@EnableSadaka", enableSadaka.HasValue ? enableSadaka.Value : DBNull.Value);
        command.Parameters.AddWithValue("@EnableZakaa", enableZakaa.HasValue ? enableZakaa.Value : DBNull.Value);
        command.Parameters.AddWithValue("@PageIndex", 0);
        command.Parameters.AddWithValue("@PageSize", Math.Clamp(pageSize, 1, MaxFetchedSponsorships));

        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var item = MapContributionProjectItem(reader, languageId);
                if (item.CountryId == QatarCountryId || !item.CountryId.HasValue)
                {
                    items.Add(item);
                }
            }
        }
        catch (SqlException ex)
        {
            _logger.LogError(ex, "Failed to fetch QC contribution projects.");
            throw;
        }

        return items;
    }

    private async Task<List<DonationItemDto>> LoadAssistanceCaseItemsAsync(
        SqlConnection connection,
        int languageId,
        string? searchQuery,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var items = new List<DonationItemDto>();

        await using var command = new SqlCommand("AssistanceCasesGet", connection)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 60
        };

        command.Parameters.AddWithValue("@MainAccountTypeId", DBNull.Value);
        command.Parameters.AddWithValue("@CountryId", QatarCountryId);
        command.Parameters.AddWithValue("@EmployeeId", OnlineEmployeeId);
        command.Parameters.AddWithValue("@minAmount", 0);
        command.Parameters.AddWithValue("@maxAmount", 0);
        command.Parameters.AddWithValue("@searchText", string.IsNullOrWhiteSpace(searchQuery) ? string.Empty : searchQuery.Trim());
        command.Parameters.AddWithValue("@CategoryTypeID", DBNull.Value);
        command.Parameters.AddWithValue("@GetClosedCauses", 0);
        command.Parameters.AddWithValue("@PageIndex", 0);
        command.Parameters.AddWithValue("@PageSize", Math.Clamp(pageSize, 1, MaxFetchedSponsorships));

        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var item = MapAssistanceCaseItem(reader, languageId);
                if (item.CountryId == QatarCountryId || !item.CountryId.HasValue)
                {
                    items.Add(item);
                }
            }
        }
        catch (SqlException ex)
        {
            _logger.LogError(ex, "Failed to fetch QC assistance cases.");
            throw;
        }

        return items;
    }

    private static ProjectItemDto MapProjectItem(SqlDataReader reader, int languageId)
    {
        var projectId = ReadFlexibleInt(reader, "ProjectId", "ProjectID") ?? 0;
        var templateId = ReadFlexibleInt(reader, "TemplateId", "TemplateID") ?? 0;
        var id = projectId == 0 ? templateId : projectId;
        var titleAr = ReadFlexibleString(reader, "TitleAr", "ArName", "ProjectName", "TemplateName") ?? string.Empty;
        var titleEn = ReadFlexibleString(reader, "TitleEn", "EnName", "ProjectNameEn", "TemplateNameEn") ?? string.Empty;
        var descriptionAr = ReadFlexibleString(reader, "DescriptionAr", "Goals", "Description", "Notes", "DescriptionAR") ?? string.Empty;
        var descriptionEn = ReadFlexibleString(reader, "DescriptionEn", "GoalsEn", "DescriptionEN", "DescriptionEn", "NotesEn") ?? string.Empty;
        var projectTypeId = ReadFlexibleInt(reader, "ProjectTypeId", "TypeId");
        var projectTypeNameAr = ReadFlexibleString(reader, "ProjectTypeName", "ProjectTypeArName", "ProjectTypeNameAr", "ArNameProjectType") ?? string.Empty;
        var projectTypeNameEn = ReadFlexibleString(reader, "ProjectTypeEnName", "ProjectTypeNameEn") ?? string.Empty;
        var countryAr = ReadFlexibleString(reader, "CountryArName", "CountryName", "Country") ?? string.Empty;
        var countryEn = ReadFlexibleString(reader, "CountryEnName", "CountryNameEn") ?? string.Empty;
        var amount = ReadFlexibleDecimal(reader, "Cost_Dec", "Cost", "Amount");
        var remaining = ReadFlexibleDecimal(reader, "Remaining", "RemainingAmount");
        var paid = ReadFlexibleDecimal(reader, "Paid", "PaidAmount");
        var image = FirstNonEmpty(
            BuildQatarCharityImageUrl(ReadFlexibleString(reader, "SmallImage")),
            BuildQatarCharityImageUrl(ReadFlexibleString(reader, "Image", "LinkImageAR", "ProjectDefaultImageURL", "ProjectDefaultImageURLValue", "DefaultAccountImage")));

        return new ProjectItemDto
        {
            Id = id,
            ProjectId = projectId,
            TemplateId = templateId,
            TitleAr = titleAr.Trim(),
            TitleEn = titleEn.Trim(),
            Title = languageId == 1 ? titleAr.Trim() : FirstNonEmpty(titleEn, titleAr) ?? string.Empty,
            DescriptionAr = descriptionAr.Trim(),
            DescriptionEn = descriptionEn.Trim(),
            Description = languageId == 1 ? descriptionAr.Trim() : FirstNonEmpty(descriptionEn, descriptionAr) ?? string.Empty,
            ProjectTypeId = projectTypeId,
            ProjectSubTypeId = ReadFlexibleInt(reader, "ProjectSubTypeId"),
            ProjectTypeName = languageId == 1 ? projectTypeNameAr.Trim() : FirstNonEmpty(projectTypeNameEn, projectTypeNameAr) ?? string.Empty,
            ProjectTypeNameAr = projectTypeNameAr.Trim(),
            ProjectTypeNameEn = projectTypeNameEn.Trim(),
            CountryId = ReadFlexibleInt(reader, "CountryId"),
            CountryAr = countryAr.Trim(),
            CountryEn = countryEn.Trim(),
            Amount = amount,
            AmountFormatted = FormatCurrency(amount) ?? string.Empty,
            PaidAmount = paid,
            PaidAmountFormatted = FormatCurrency(paid) ?? string.Empty,
            RemainingAmount = remaining,
            RemainingAmountFormatted = FormatCurrency(remaining) ?? string.Empty,
            Image = image ?? string.Empty,
            MainAccountTypeId = ReadFlexibleInt(reader, "MainAccountTypeId"),
            AccountTypeId = ReadFlexibleInt(reader, "AccountTypeId3", "AccountTypeId"),
            AcceptsSadaka = ReadFlexibleBool(reader, "EnableSadaka") ?? false,
            AcceptsZakaa = ReadFlexibleBool(reader, "EnableZakaa") ?? false,
            Source = "ProjectsAvailableGet"
        };
    }

    private static DonationItemDto MapContributionProjectItem(SqlDataReader reader, int languageId)
    {
        var projectId = ReadFlexibleInt(reader, "ProjectId", "ProjectID") ?? 0;
        var templateId = ReadFlexibleInt(reader, "TemplateId", "TemplateID") ?? 0;
        var id = projectId == 0 ? templateId : projectId;
        var titleAr = ReadFlexibleString(reader, "ArName", "TitleAr", "ProjectName", "TemplateName", "AccountNameLevel3") ?? string.Empty;
        var titleEn = ReadFlexibleString(reader, "EnName", "TitleEn", "ProjectNameEn", "TemplateNameEn", "AccountEnNameLevel3") ?? string.Empty;
        var descriptionAr = ReadFlexibleString(reader, "Goals", "DescriptionAr", "Description", "Notes") ?? string.Empty;
        var descriptionEn = ReadFlexibleString(reader, "GoalsEn", "DescriptionEn", "DescriptionEN", "NotesEn") ?? string.Empty;
        var amount = ReadFlexibleDecimal(reader, "Cost_Dec", "Cost", "Amount");
        var remaining = ReadFlexibleDecimal(reader, "Remaining", "RemainingAmount");
        var paid = ReadFlexibleDecimal(reader, "Paid", "PaidAmount", "PreviousSumAmount");
        var mainAccountTypeId = ReadFlexibleInt(reader, "MainAccountTypeId", "AccountTypeID1");
        var categoryAr = ReadFlexibleString(reader, "AccountNameLevel1", "AppearAsLevel1", "ProjectTypeArName", "ProjectTypeName") ?? string.Empty;
        var categoryEn = ReadFlexibleString(reader, "AccountEnNameLevel1", "AppearAsLevel1En", "ProjectTypeEnName", "ProjectTypeNameEn") ?? string.Empty;
        var image = FirstNonEmpty(
            BuildQatarCharityImageUrl(ReadFlexibleString(reader, "Image", "LinkImageAR", "ProjectDefaultImageURL", "ProjectDefaultImageURLValue")),
            BuildQatarCharityImageUrl(ReadFlexibleString(reader, "WebIcon", "Icon", "DefaultAccountImage")));

        return new DonationItemDto
        {
            Id = id,
            PaidThroughId = 4,
            PaidForId = projectId,
            TypeId = 1,
            DonationsTypeId = 4,
            TitleAr = titleAr.Trim(),
            TitleEn = titleEn.Trim(),
            Title = languageId == 1 ? titleAr.Trim() : FirstNonEmpty(titleEn, titleAr) ?? string.Empty,
            DescriptionAr = descriptionAr.Trim(),
            DescriptionEn = descriptionEn.Trim(),
            Description = languageId == 1 ? descriptionAr.Trim() : FirstNonEmpty(descriptionEn, descriptionAr) ?? string.Empty,
            CategoryAr = categoryAr.Trim(),
            CategoryEn = categoryEn.Trim(),
            Category = languageId == 1 ? categoryAr.Trim() : FirstNonEmpty(categoryEn, categoryAr) ?? string.Empty,
            CountryId = ReadFlexibleInt(reader, "CountryId"),
            CountryAr = ReadFlexibleString(reader, "CountryArName", "CountryName", "Country") ?? string.Empty,
            CountryEn = ReadFlexibleString(reader, "CountryEnName", "CountryNameEn") ?? string.Empty,
            Amount = amount,
            AmountFormatted = FormatCurrency(amount) ?? string.Empty,
            PaidAmount = paid,
            PaidAmountFormatted = FormatCurrency(paid) ?? string.Empty,
            RemainingAmount = remaining,
            RemainingAmountFormatted = FormatCurrency(remaining) ?? string.Empty,
            Image = image ?? string.Empty,
            MainAccountTypeId = mainAccountTypeId,
            AccountTypeId = ReadFlexibleInt(reader, "AccountTypeId3", "AccountTypeId"),
            ProjectTypeId = ReadFlexibleInt(reader, "ProjectTypeId"),
            ProjectSubTypeId = ReadFlexibleInt(reader, "ProjectSubTypeId"),
            AcceptsSadaka = ReadFlexibleBool(reader, "EnableSadaka") ?? false,
            AcceptsZakaa = ReadFlexibleBool(reader, "EnableZakaa") ?? false,
            Source = "ContributionProjectsGetV2"
        };
    }

    private static DonationItemDto MapAssistanceCaseItem(SqlDataReader reader, int languageId)
    {
        var id = ReadFlexibleInt(reader, "AssistanceCaseId", "AssistanceCaseID", "PaidForId") ?? 0;
        var titleAr = ReadFlexibleString(reader, "PersonName", "shortName", "CategoryTypeName", "AccountName") ?? string.Empty;
        var titleEn = ReadFlexibleString(reader, "PersonNameEn", "shortNameEn", "CategoryTypeNameEn", "AccountNameEn") ?? string.Empty;
        var descriptionAr = ReadFlexibleString(reader, "Notes", "DescriptionAr", "Description") ?? string.Empty;
        var descriptionEn = ReadFlexibleString(reader, "NotesEn", "DescriptionEn", "DescriptionEN") ?? string.Empty;
        var categoryAr = ReadFlexibleString(reader, "CategoryTypeName", "AccountName", "AccountNameLevel1") ?? string.Empty;
        var categoryEn = ReadFlexibleString(reader, "CategoryTypeNameEn", "AccountNameEn", "AccountEnNameLevel1") ?? string.Empty;
        var amount = ReadFlexibleDecimal(reader, "Amount", "TotalCost", "Cost");
        var remaining = ReadFlexibleDecimal(reader, "Remaining", "RemainingAmount");
        var paid = amount.HasValue && remaining.HasValue ? Math.Max(amount.Value - remaining.Value, 0) : ReadFlexibleDecimal(reader, "Paid", "PaidAmount", "PreviousSumAmount");
        var image = FirstNonEmpty(
            BuildQatarCharityImageUrl(ReadFlexibleString(reader, "Image", "LinkImageAR", "LargeImage")),
            BuildQatarCharityImageUrl(ReadFlexibleString(reader, "WebIcon", "Icon")));

        return new DonationItemDto
        {
            Id = id,
            PaidThroughId = 7,
            PaidForId = id,
            TypeId = 1,
            DonationsTypeId = 3,
            TitleAr = titleAr.Trim(),
            TitleEn = titleEn.Trim(),
            Title = languageId == 1 ? titleAr.Trim() : FirstNonEmpty(titleEn, titleAr) ?? string.Empty,
            DescriptionAr = descriptionAr.Trim(),
            DescriptionEn = descriptionEn.Trim(),
            Description = languageId == 1 ? descriptionAr.Trim() : FirstNonEmpty(descriptionEn, descriptionAr) ?? string.Empty,
            CategoryAr = categoryAr.Trim(),
            CategoryEn = categoryEn.Trim(),
            Category = languageId == 1 ? categoryAr.Trim() : FirstNonEmpty(categoryEn, categoryAr) ?? string.Empty,
            CountryId = ReadFlexibleInt(reader, "CountryId"),
            CountryAr = ReadFlexibleString(reader, "CountryArName", "CountryName", "Country") ?? string.Empty,
            CountryEn = ReadFlexibleString(reader, "CountryEnName", "CountryNameEn") ?? string.Empty,
            Amount = amount,
            AmountFormatted = FormatCurrency(amount) ?? string.Empty,
            PaidAmount = paid,
            PaidAmountFormatted = FormatCurrency(paid) ?? string.Empty,
            RemainingAmount = remaining,
            RemainingAmountFormatted = FormatCurrency(remaining) ?? string.Empty,
            Image = image ?? string.Empty,
            MainAccountTypeId = ReadFlexibleInt(reader, "MainAccountTypeId", "AccountTypeID1"),
            AccountTypeId = ReadFlexibleInt(reader, "AccountTypeId3", "AccountTypeId"),
            NeedTypeId = ReadFlexibleInt(reader, "CategoryTypeID", "NeedTypeId"),
            AcceptsSadaka = ReadFlexibleBool(reader, "EnableSadaka") ?? false,
            AcceptsZakaa = ReadFlexibleBool(reader, "EnableZakaa") ?? false,
            Source = "AssistanceCasesGet"
        };
    }

    private static SponsorshipItemDto MapSponsorshipItem(SqlDataReader reader, int languageId)
    {
        var sponsoredId = ReadInt(reader, "SponsoredId") ?? 0;
        var categoryId = ReadInt(reader, "CategoryId");
        var categoryNameAr = ReadString(reader, "CategoryName");
        var categoryNameEn = ReadString(reader, "CategoryNameEng");
        var accountLevelAr = ReadString(reader, "AppearAsLevel1");
        var accountLevelEn = ReadString(reader, "AppearAsLevel1En");
        var nameAr = ReadString(reader, "Name");
        var nameEn = FirstNonEmpty(ReadString(reader, "NativeName"), nameAr);
        var titleAr = FirstNonEmpty(nameAr, nameEn, $"حالة كفالة #{sponsoredId}");
        var titleEn = FirstNonEmpty(nameEn, nameAr, $"Sponsorship case #{sponsoredId}");
        var title = languageId == 1 ? titleAr : titleEn;
        var categoryLabel = languageId == 1 ? categoryNameAr : FirstNonEmpty(categoryNameEn, categoryNameAr);
        var accountLabel = languageId == 1 ? accountLevelAr : FirstNonEmpty(accountLevelEn, accountLevelAr);
        var amount = ReadDecimal(reader, "SponsorshipAmount");
        var countryAr = ReadString(reader, "CountryName");
        var countryEn = ReadString(reader, "CountryNameEng");
        var image = FirstNonEmpty(ReadString(reader, "Image3"), ReadString(reader, "Photo"));
        var description = string.Join(" | ", new[] { accountLabel, categoryLabel }.Where(value => !string.IsNullOrWhiteSpace(value)));
        var waitingPeriod = ReadInt(reader, "WaitingPeriod");
        var birthDate = ReadDateTime(reader, "BirthDate");

        return new SponsorshipItemDto
        {
            Id = sponsoredId,
            SponsoredId = sponsoredId,
            PaidForId = sponsoredId,
            SponsoredCode = ReadString(reader, "SponsoredCode"),
            RequestId = ReadInt(reader, "ApplicationId"),
            Name = title,
            Title = title,
            TitleAr = titleAr,
            TitleEn = titleEn,
            ShortName = BuildShortName(title),
            ShortNameAr = BuildShortName(nameAr),
            ShortNameEn = BuildShortName(nameEn),
            Subtitle = description,
            Description = description,
            Notes = description,
            NotesAr = description,
            Summary = description,
            BirthDate = birthDate,
            Sex = ReadString(reader, "Sex"),
            SexLabel = FormatSex(ReadString(reader, "Sex"), languageId),
            Category = new SponsorshipCategoryDto
            {
                Id = categoryId,
                Key = BuildCategoryKey(categoryId),
                Label = categoryLabel,
                IconKey = BuildCategoryKey(categoryId),
                Icon = BuildCategoryIcon(categoryId)
            },
            CategoryId = categoryId,
            CategoryName = categoryNameAr,
            CategoryNameEng = categoryNameEn,
            SponsorshipCategoryId = categoryId,
            CategoryTypeId = categoryId,
            TypeId = 3,
            AccountTypeId = ReadInt(reader, "AccountTypeId3"),
            MainAccountTypeId = ReadInt(reader, "AccountTypeID1"),
            Amount = amount,
            SponsorshipAmount = amount,
            MonthlyAmount = amount,
            AmountFormatted = FormatCurrency(amount),
            SponsorshipAmountFormatted = FormatCurrency(amount),
            RemainingAmount = amount,
            RemainingAmountFormatted = FormatCurrency(amount),
            DonorCount = 0,
            FundedAmount = 0,
            FundedAmountFormatted = FormatCurrency(0),
            ProgressPercentage = 0,
            IsActive = true,
            HideFromOnline = false,
            IsAvailable = true,
            CtaLabel = languageId == 1 ? "اكفل الآن" : "Sponsor now",
            IconKey = BuildCategoryKey(categoryId),
            Icon = BuildCategoryIcon(categoryId),
            City = ReadString(reader, "City"),
            Country = languageId == 1 ? countryAr : FirstNonEmpty(countryEn, countryAr),
            CountryName = countryAr,
            CountryNameEng = countryEn,
            CountryId = ReadInt(reader, "CountryId"),
            LocationText = languageId == 1 ? countryAr : FirstNonEmpty(countryEn, countryAr),
            WaitingPeriod = waitingPeriod,
            WaitingPeriodLabel = waitingPeriod.HasValue && waitingPeriod.Value > 0
                ? (languageId == 1 ? $"ينتظر منذ {waitingPeriod.Value} يوم" : $"Waiting for {waitingPeriod.Value} days")
                : null,
            Image = image,
            LicenseNumber = ReadString(reader, "ProductLicenseNumber"),
            DynamicsCode = ReadString(reader, "SponsoredCode"),
            CreatedDate = null,
            FatherAlive = ReadBool(reader, "IsFatherLive"),
            FatherJob = ReadString(reader, "FatherJob"),
            MotherAlive = ReadBool(reader, "IsMotherLive"),
            MotherJob = ReadString(reader, "MotherJob"),
            DisabilityCause = ReadString(reader, "DisabilityCause"),
            TeacherLanguages = ReadString(reader, "TeacherLanguages"),
            IsFullCoverage = ReadBool(reader, "IsFullCoverage"),
            IsOrphanStudent = ReadBool(reader, "IsOrphanStudent"),
            Source = "GetNonSponsorships"
        };
    }

    private static List<SponsorshipTabDto> BuildTabs(
        IReadOnlyList<SponsorshipCategoryRow> categories,
        IReadOnlyList<SponsorshipItemDto> items,
        int? selectedCategoryId,
        int languageId)
    {
        var counts = items
            .Where(item => item.CategoryTypeId.HasValue)
            .GroupBy(item => item.CategoryTypeId!.Value)
            .ToDictionary(group => group.Key, group => group.Count());

        var tabs = new List<SponsorshipTabDto>
        {
            new()
            {
                Id = null,
                Key = "all",
                Label = languageId == 1 ? "الكل" : "All",
                LabelAr = "الكل",
                LabelEn = "All",
                Count = items.Count,
                IsAll = true,
                IsActive = !selectedCategoryId.HasValue,
                IconKey = "all",
                Icon = "grid"
            }
        };

        tabs.AddRange(categories
            .Where(category => category.Id > 0)
            .Select(category => new SponsorshipTabDto
            {
                Id = category.Id,
                Key = BuildCategoryKey(category.Id),
                Label = category.Name,
                LabelAr = category.Name,
                LabelEn = category.Name,
                Count = counts.TryGetValue(category.Id, out var count) ? count : 0,
                IsAll = false,
                IsActive = selectedCategoryId == category.Id,
                IconKey = BuildCategoryKey(category.Id),
                Icon = BuildCategoryIcon(category.Id)
            }));

        return tabs;
    }

    private static List<SponsorshipTabDto> BuildProjectTabs(
        IReadOnlyList<ProjectItemDto> items,
        int? selectedProjectTypeId,
        int languageId)
    {
        var tabs = new List<SponsorshipTabDto>
        {
            new()
            {
                Id = null,
                Key = "all",
                Label = languageId == 1 ? "الكل" : "All",
                LabelAr = "الكل",
                LabelEn = "All",
                Count = items.Count,
                IsAll = true,
                IsActive = !selectedProjectTypeId.HasValue,
                IconKey = "all",
                Icon = "grid"
            }
        };

        tabs.AddRange(items
            .Where(item => item.ProjectTypeId.HasValue && item.ProjectTypeId.Value > 0)
            .GroupBy(item => new
            {
                Id = item.ProjectTypeId!.Value,
                LabelAr = FirstNonEmpty(item.ProjectTypeNameAr, item.ProjectTypeName),
                LabelEn = FirstNonEmpty(item.ProjectTypeNameEn, item.ProjectTypeNameAr, item.ProjectTypeName)
            })
            .OrderBy(group => group.Key.Id)
            .Select(group => new SponsorshipTabDto
            {
                Id = group.Key.Id,
                Key = group.Key.Id.ToString(),
                Label = languageId == 1 ? group.Key.LabelAr : group.Key.LabelEn,
                LabelAr = group.Key.LabelAr,
                LabelEn = group.Key.LabelEn,
                Count = group.Count(),
                IsAll = false,
                IsActive = selectedProjectTypeId == group.Key.Id,
                IconKey = group.Key.Id.ToString(),
                Icon = group.FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.Image))?.Image
            }));

        return tabs;
    }

    private static List<SponsorshipTabDto> BuildContributionTabs(
        IReadOnlyList<DonationItemDto> items,
        int? selectedCategoryId,
        int languageId)
    {
        var tabs = new List<SponsorshipTabDto>
        {
            new()
            {
                Id = null,
                Key = "all",
                Label = languageId == 1 ? "الكل" : "All",
                LabelAr = "الكل",
                LabelEn = "All",
                Count = items.Count,
                IsAll = true,
                IsActive = !selectedCategoryId.HasValue,
                IconKey = "all",
                Icon = "grid"
            }
        };

        tabs.AddRange(items
            .Where(item => item.MainAccountTypeId.HasValue && item.MainAccountTypeId.Value > 0)
            .GroupBy(item => new
            {
                Id = item.MainAccountTypeId!.Value,
                LabelAr = FirstNonEmpty(item.CategoryAr, item.Category),
                LabelEn = FirstNonEmpty(item.CategoryEn, item.CategoryAr, item.Category)
            })
            .OrderBy(group => group.Key.Id)
            .Select(group => new SponsorshipTabDto
            {
                Id = group.Key.Id,
                Key = group.Key.Id.ToString(),
                Label = languageId == 1 ? group.Key.LabelAr : group.Key.LabelEn,
                LabelAr = group.Key.LabelAr,
                LabelEn = group.Key.LabelEn,
                Count = group.Count(),
                IsAll = false,
                IsActive = selectedCategoryId == group.Key.Id,
                IconKey = group.Key.Id.ToString(),
                Icon = group.FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.Image))?.Image
            }));

        return tabs;
    }

    private static List<SponsorshipTabDto> BuildAssistanceTabs(
        IReadOnlyList<DonationItemDto> items,
        int? selectedNeedTypeId,
        int languageId)
    {
        var tabs = new List<SponsorshipTabDto>
        {
            new()
            {
                Id = null,
                Key = "all",
                Label = languageId == 1 ? "الكل" : "All",
                LabelAr = "الكل",
                LabelEn = "All",
                Count = items.Count,
                IsAll = true,
                IsActive = !selectedNeedTypeId.HasValue,
                IconKey = "all",
                Icon = "grid"
            }
        };

        tabs.AddRange(items
            .Where(item => item.NeedTypeId.HasValue && item.NeedTypeId.Value > 0)
            .GroupBy(item => new
            {
                Id = item.NeedTypeId!.Value,
                LabelAr = FirstNonEmpty(item.CategoryAr, item.Category),
                LabelEn = FirstNonEmpty(item.CategoryEn, item.CategoryAr, item.Category)
            })
            .OrderBy(group => group.Key.Id)
            .Select(group => new SponsorshipTabDto
            {
                Id = group.Key.Id,
                Key = group.Key.Id.ToString(),
                Label = languageId == 1 ? group.Key.LabelAr : group.Key.LabelEn,
                LabelAr = group.Key.LabelAr,
                LabelEn = group.Key.LabelEn,
                Count = group.Count(),
                IsAll = false,
                IsActive = selectedNeedTypeId == group.Key.Id,
                IconKey = group.Key.Id.ToString(),
                Icon = group.FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.Image))?.Image
            }));

        return tabs;
    }

    private static List<SponsorshipItemDto> ApplySort(List<SponsorshipItemDto> items, string sortBy) => sortBy switch
    {
        "oldest" => items.OrderBy(item => item.WaitingPeriod ?? 0).ThenBy(item => item.Id).ToList(),
        "amount_asc" => items.OrderBy(item => item.Amount ?? 0).ThenBy(item => item.Id).ToList(),
        "amount_desc" => items.OrderByDescending(item => item.Amount ?? 0).ThenBy(item => item.Id).ToList(),
        "remaining_asc" => items.OrderBy(item => item.RemainingAmount ?? 0).ThenBy(item => item.Id).ToList(),
        "remaining_desc" => items.OrderByDescending(item => item.RemainingAmount ?? 0).ThenBy(item => item.Id).ToList(),
        "donors_desc" => items.OrderByDescending(item => item.DonorCount).ThenBy(item => item.Id).ToList(),
        _ => items.OrderByDescending(item => item.WaitingPeriod ?? 0).ThenBy(item => item.Id).ToList()
    };

    private static List<ProjectItemDto> ApplyProjectSort(List<ProjectItemDto> items, string sortBy) => sortBy switch
    {
        "oldest" => items.OrderBy(item => item.Id).ToList(),
        "amount_asc" => items.OrderBy(item => item.Amount ?? 0).ThenBy(item => item.Id).ToList(),
        "amount_desc" => items.OrderByDescending(item => item.Amount ?? 0).ThenBy(item => item.Id).ToList(),
        "remaining_asc" => items.OrderBy(item => item.RemainingAmount ?? 0).ThenBy(item => item.Id).ToList(),
        "remaining_desc" => items.OrderByDescending(item => item.RemainingAmount ?? 0).ThenBy(item => item.Id).ToList(),
        _ => items.OrderByDescending(item => item.Id).ToList()
    };

    private static List<DonationItemDto> ApplyGeneralDonationSort(List<DonationItemDto> items, string sortBy, bool mostWaiting) => sortBy switch
    {
        "oldest" => items.OrderBy(item => item.Id).ToList(),
        "amount_asc" => items.OrderBy(item => item.Amount ?? 0).ThenBy(item => item.Id).ToList(),
        "amount_desc" => items.OrderByDescending(item => item.Amount ?? 0).ThenBy(item => item.Id).ToList(),
        "remaining_asc" => items.OrderBy(item => item.RemainingAmount ?? 0).ThenByDescending(item => item.Id).ToList(),
        "remaining_desc" => items.OrderByDescending(item => item.RemainingAmount ?? 0).ThenBy(item => item.Id).ToList(),
        "donors_desc" => items.OrderByDescending(item => item.DonorCount ?? 0).ThenBy(item => item.Id).ToList(),
        _ when mostWaiting => items.OrderBy(item => item.RemainingAmount ?? decimal.MaxValue).ThenByDescending(item => item.Id).ToList(),
        _ => items.OrderByDescending(item => item.Id).ToList()
    };

    private static object ToGeneralDonationItem(DonationItemDto item) => new
    {
        id = item.Id,
        paidThroughId = item.PaidThroughId,
        paidForId = item.PaidForId,
        typeId = item.TypeId,
        donationsTypeId = item.DonationsTypeId,
        title = item.Title,
        titleAr = item.TitleAr,
        titleEn = item.TitleEn,
        description = item.Description,
        descriptionAr = item.DescriptionAr,
        descriptionEn = item.DescriptionEn,
        category = item.Category,
        categoryAr = item.CategoryAr,
        categoryEn = item.CategoryEn,
        countryId = item.CountryId,
        country = item.CountryAr,
        countryAr = item.CountryAr,
        countryEn = item.CountryEn,
        amount = item.Amount,
        amountFormatted = item.AmountFormatted,
        paidAmount = item.PaidAmount,
        paidAmountFormatted = item.PaidAmountFormatted,
        remainingAmount = item.RemainingAmount,
        remainingAmountFormatted = item.RemainingAmountFormatted,
        fundedAmount = item.PaidAmount,
        fundedAmountFormatted = item.PaidAmountFormatted,
        progressPercentage = ResolveGeneralDonationProgress(item),
        progressPercentageRaw = ResolveGeneralDonationProgress(item),
        image = NormalizeQatarCharityExportPath(item.Image),
        waitingPeriod = (int?)null,
        waitingPeriodLabel = string.Empty,
        acceptsSadaka = item.AcceptsSadaka,
        acceptsZakaa = item.AcceptsZakaa,
        mainAccountTypeId = item.MainAccountTypeId,
        accountTypeId = item.AccountTypeId,
        projectTypeId = item.ProjectTypeId,
        projectSubTypeId = item.ProjectSubTypeId,
        sponsorshipCategoryId = (int?)null,
        needTypeId = item.NeedTypeId,
        source = item.Source
    };

    private static object ToProjectDonationItem(ProjectItemDto item) => new
    {
        id = item.Id,
        paidThroughId = 4,
        paidForId = item.ProjectId == 0 ? item.TemplateId : item.ProjectId,
        typeId = 2,
        donationsTypeId = 0,
        title = item.Title,
        titleAr = item.TitleAr,
        titleEn = item.TitleEn,
        description = item.Description,
        descriptionAr = item.DescriptionAr,
        descriptionEn = item.DescriptionEn,
        category = item.ProjectTypeName,
        categoryAr = item.ProjectTypeNameAr,
        categoryEn = item.ProjectTypeNameEn,
        projectTypeName = item.ProjectTypeName,
        projectTypeNameAr = item.ProjectTypeNameAr,
        projectTypeNameEn = item.ProjectTypeNameEn,
        countryId = item.CountryId,
        country = item.CountryAr,
        countryAr = item.CountryAr,
        countryEn = item.CountryEn,
        amount = item.Amount,
        amountFormatted = item.AmountFormatted,
        paidAmount = item.PaidAmount,
        paidAmountFormatted = item.PaidAmountFormatted,
        remainingAmount = item.RemainingAmount,
        remainingAmountFormatted = item.RemainingAmountFormatted,
        fundedAmount = item.PaidAmount,
        fundedAmountFormatted = item.PaidAmountFormatted,
        progressPercentage = ResolveProjectProgress(item),
        progressPercentageRaw = ResolveProjectProgress(item),
        image = NormalizeQatarCharityExportPath(item.Image),
        waitingPeriod = (int?)null,
        waitingPeriodLabel = string.Empty,
        acceptsSadaka = item.AcceptsSadaka,
        acceptsZakaa = item.AcceptsZakaa,
        mainAccountTypeId = item.MainAccountTypeId,
        accountTypeId = item.AccountTypeId,
        projectTypeId = item.ProjectTypeId,
        projectSubTypeId = item.ProjectSubTypeId,
        sponsorshipCategoryId = (int?)null,
        needTypeId = (int?)null,
        source = item.Source
    };

    private static object ToDonationItem(SponsorshipItemDto item) => new
    {
        id = item.Id,
        paidThroughId = 1,
        paidForId = item.PaidForId,
        typeId = 3,
        donationsTypeId = 0,
        title = item.Title,
        titleAr = item.TitleAr,
        titleEn = item.TitleEn,
        description = item.Description,
        descriptionAr = item.NotesAr,
        descriptionEn = item.NotesEn,
        category = item.Category?.Label,
        categoryAr = item.CategoryName,
        categoryEn = item.CategoryNameEng,
        countryId = item.CountryId,
        country = item.Country,
        countryAr = item.CountryName,
        countryEn = item.CountryNameEng,
        amount = item.Amount,
        amountFormatted = item.AmountFormatted,
        paidAmount = item.FundedAmount,
        paidAmountFormatted = item.FundedAmountFormatted,
        remainingAmount = item.RemainingAmount,
        remainingAmountFormatted = item.RemainingAmountFormatted,
        fundedAmount = item.FundedAmount,
        fundedAmountFormatted = item.FundedAmountFormatted,
        progressPercentage = item.ProgressPercentage,
        progressPercentageRaw = item.ProgressPercentage,
        image = item.Image,
        waitingPeriod = item.WaitingPeriod,
        waitingPeriodLabel = item.WaitingPeriodLabel,
        acceptsSadaka = false,
        acceptsZakaa = false,
        mainAccountTypeId = item.MainAccountTypeId,
        accountTypeId = item.AccountTypeId,
        projectTypeId = (int?)null,
        projectSubTypeId = (int?)null,
        sponsorshipCategoryId = item.SponsorshipCategoryId,
        needTypeId = (int?)null,
        source = item.Source
    };

    private static int ResolveGeneralDonationProgress(DonationItemDto item)
    {
        if (!item.Amount.HasValue || item.Amount.Value <= 0 || !item.RemainingAmount.HasValue)
        {
            return 0;
        }

        var paid = item.PaidAmount ?? Math.Max(item.Amount.Value - item.RemainingAmount.Value, 0);
        return (int)Math.Clamp(Math.Round(paid / item.Amount.Value * 100m), 0, 100);
    }

    private static int ResolveProjectProgress(ProjectItemDto item)
    {
        if (!item.Amount.HasValue || item.Amount.Value <= 0 || !item.RemainingAmount.HasValue)
        {
            return 0;
        }

        var paid = item.PaidAmount ?? Math.Max(item.Amount.Value - item.RemainingAmount.Value, 0);
        return (int)Math.Clamp(Math.Round(paid / item.Amount.Value * 100m), 0, 100);
    }

    private static int NormalizeLanguageId(int languageId) => languageId == 2 ? 2 : 1;

    private static int NormalizeDonationTypeId(int donationsTypeId) => donationsTypeId == 3 ? 3 : 4;

    private static int ResolveLanguageId(string? language, int? languageId)
    {
        if (!string.IsNullOrWhiteSpace(language) && language.Trim().Equals("en", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        return NormalizeLanguageId(languageId.GetValueOrDefault(1));
    }

    private static int? NormalizePositive(int? value) => value.HasValue && value.Value > 0 ? value.Value : null;

    private static string? NormalizeSearch(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormalizeSort(string? sortBy)
    {
        var normalized = sortBy?.Trim().ToLowerInvariant();
        return normalized is "oldest" or "amount_asc" or "amount_desc" or "remaining_asc" or "remaining_desc" or "donors_desc"
            ? normalized
            : "newest";
    }

    private static string BuildQatarCharityImageUrl(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var trimmed = path.Trim();
        if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return NormalizeQatarCharityExportPath(trimmed);
        }

        var relativePath = trimmed.TrimStart('/');
        if (relativePath.StartsWith("ExportPath/", StringComparison.OrdinalIgnoreCase))
        {
            relativePath = relativePath["ExportPath/".Length..];
        }

        return NormalizeQatarCharityExportPath(QatarCharityExportPathBaseUrl + relativePath);
    }

    private static string NormalizeQatarCharityExportPath(string? url) =>
        string.IsNullOrWhiteSpace(url)
            ? string.Empty
            : url.Trim()
                .Replace("ExportPath/ExportPath/", "ExportPath/", StringComparison.OrdinalIgnoreCase)
                .Replace("ExportPath//ExportPath/", "ExportPath/", StringComparison.OrdinalIgnoreCase);

    private static bool HasColumn(SqlDataReader reader, string columnName)
    {
        for (var index = 0; index < reader.FieldCount; index++)
        {
            if (string.Equals(reader.GetName(index), columnName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string? ReadFlexibleString(SqlDataReader reader, params string[] columnNames)
    {
        foreach (var columnName in columnNames)
        {
            if (!HasColumn(reader, columnName))
            {
                continue;
            }

            var ordinal = reader.GetOrdinal(columnName);
            if (!reader.IsDBNull(ordinal))
            {
                return Convert.ToString(reader.GetValue(ordinal))?.Trim();
            }
        }

        return null;
    }

    private static int? ReadFlexibleInt(SqlDataReader reader, params string[] columnNames)
    {
        foreach (var columnName in columnNames)
        {
            if (!HasColumn(reader, columnName))
            {
                continue;
            }

            var ordinal = reader.GetOrdinal(columnName);
            if (!reader.IsDBNull(ordinal))
            {
                return Convert.ToInt32(reader.GetValue(ordinal));
            }
        }

        return null;
    }

    private static decimal? ReadFlexibleDecimal(SqlDataReader reader, params string[] columnNames)
    {
        foreach (var columnName in columnNames)
        {
            if (!HasColumn(reader, columnName))
            {
                continue;
            }

            var ordinal = reader.GetOrdinal(columnName);
            if (!reader.IsDBNull(ordinal) && decimal.TryParse(Convert.ToString(reader.GetValue(ordinal)), out var value))
            {
                return value;
            }
        }

        return null;
    }

    private static bool? ReadFlexibleBool(SqlDataReader reader, params string[] columnNames)
    {
        foreach (var columnName in columnNames)
        {
            if (!HasColumn(reader, columnName))
            {
                continue;
            }

            var ordinal = reader.GetOrdinal(columnName);
            if (!reader.IsDBNull(ordinal))
            {
                return Convert.ToBoolean(reader.GetValue(ordinal));
            }
        }

        return null;
    }

    private static string BuildCategoryKey(int? id) => id switch
    {
        1 => "orphans",
        2 => "families",
        3 => "special-needs",
        5 => "teachers",
        8 => "students",
        null => "all",
        _ => $"category-{id.Value}"
    };

    private static string BuildCategoryIcon(int? id) => id switch
    {
        1 => "heart-handshake",
        2 => "home-heart",
        3 => "accessibility",
        5 => "graduation-cap",
        8 => "book-open",
        _ => "hands-helping"
    };

    private static string? FirstNonEmpty(params string?[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static string? BuildShortName(string? value)
    {
        var parts = value?.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts is null || parts.Length == 0)
        {
            return null;
        }

        return string.Join('.', parts.Take(3).Select(part => part[0])) + ".";
    }

    private static string? FormatSex(string? value, int languageId) => value?.Trim().ToUpperInvariant() switch
    {
        "M" => languageId == 1 ? "ذكر" : "Male",
        "F" => languageId == 1 ? "أنثى" : "Female",
        _ => null
    };

    private static string FormatCurrency(decimal? value) => value.HasValue ? $"{value.Value:N0} ر.ق" : string.Empty;

    private static string? ReadString(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : Convert.ToString(reader.GetValue(ordinal))?.Trim();
    }

    private static int? ReadInt(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : Convert.ToInt32(reader.GetValue(ordinal));
    }

    private static decimal? ReadDecimal(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : Convert.ToDecimal(reader.GetValue(ordinal));
    }

    private static bool? ReadBool(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : Convert.ToBoolean(reader.GetValue(ordinal));
    }

    private static DateTime? ReadDateTime(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : Convert.ToDateTime(reader.GetValue(ordinal));
    }

    private sealed record SponsorshipCategoryRow(int Id, string Name);

    private sealed class SponsorshipsResponse
    {
        public required List<SponsorshipTabDto> Tabs { get; init; }

        public required object Filters { get; init; }

        public required List<SponsorshipItemDto> Items { get; init; }

        public required SponsorshipPagination Pagination { get; init; }

        public required object Meta { get; init; }
    }

    private sealed class ProjectsResponse
    {
        public required List<SponsorshipTabDto> Tabs { get; init; }

        public required object Filters { get; init; }

        public required List<ProjectItemDto> Items { get; init; }

        public required SponsorshipPagination Pagination { get; init; }

        public required object Meta { get; init; }
    }

    private sealed class DonationItemsResponse
    {
        public required List<SponsorshipTabDto> Tabs { get; init; }

        public required object Filters { get; init; }

        public required object Ui { get; init; }

        public required List<DonationItemDto> Items { get; init; }

        public required SponsorshipPagination Pagination { get; init; }

        public required object Meta { get; init; }
    }

    private sealed class SponsorshipTabDto
    {
        public int? Id { get; set; }

        public string? Key { get; set; }

        public string? Label { get; set; }

        public string? LabelAr { get; set; }

        public string? LabelEn { get; set; }

        public int Count { get; set; }

        public bool IsAll { get; set; }

        public bool IsActive { get; set; }

        public string? IconKey { get; set; }

        public string? Icon { get; set; }
    }

    private sealed class SponsorshipCategoryDto
    {
        public int? Id { get; set; }

        public string? Key { get; set; }

        public string? Label { get; set; }

        public string? IconKey { get; set; }

        public string? Icon { get; set; }
    }

    private sealed class SponsorshipPagination
    {
        public int PageNumber { get; set; }

        public int PageSize { get; set; }

        public int TotalItems { get; set; }

        public int TotalPages { get; set; }

        public bool HasPreviousPage { get; set; }

        public bool HasNextPage { get; set; }
    }

    private sealed class SponsorshipItemDto
    {
        public int Id { get; set; }
        public int? SponsoredId { get; set; }
        public int? PaidForId { get; set; }
        public string? SponsoredCode { get; set; }
        public int? RequestId { get; set; }
        public string? Name { get; set; }
        public string? Title { get; set; }
        public string? TitleAr { get; set; }
        public string? TitleEn { get; set; }
        public string? ShortName { get; set; }
        public string? ShortNameAr { get; set; }
        public string? ShortNameEn { get; set; }
        public string? Subtitle { get; set; }
        public string? Description { get; set; }
        public string? Notes { get; set; }
        public string? NotesAr { get; set; }
        public string? NotesEn { get; set; }
        public string? Summary { get; set; }
        public DateTime? BirthDate { get; set; }
        public string? Sex { get; set; }
        public string? SexLabel { get; set; }
        public SponsorshipCategoryDto? Category { get; set; }
        public int? CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public string? CategoryNameEng { get; set; }
        public int? SponsorshipCategoryId { get; set; }
        public int? CategoryTypeId { get; set; }
        public int TypeId { get; set; }
        public int? AccountTypeId { get; set; }
        public int? MainAccountTypeId { get; set; }
        public decimal? Amount { get; set; }
        public decimal? SponsorshipAmount { get; set; }
        public decimal? MonthlyAmount { get; set; }
        public string? AmountFormatted { get; set; }
        public string? SponsorshipAmountFormatted { get; set; }
        public decimal? RemainingAmount { get; set; }
        public string? RemainingAmountFormatted { get; set; }
        public int DonorCount { get; set; }
        public decimal? FundedAmount { get; set; }
        public string? FundedAmountFormatted { get; set; }
        public int ProgressPercentage { get; set; }
        public bool? IsActive { get; set; }
        public bool? HideFromOnline { get; set; }
        public bool? IsAvailable { get; set; }
        public string? CtaLabel { get; set; }
        public string? IconKey { get; set; }
        public string? Icon { get; set; }
        public string? City { get; set; }
        public string? Country { get; set; }
        public string? CountryName { get; set; }
        public string? CountryNameEng { get; set; }
        public int? CountryId { get; set; }
        public string? LocationText { get; set; }
        public int? WaitingPeriod { get; set; }
        public string? WaitingPeriodLabel { get; set; }
        public string? Image { get; set; }
        public string? LicenseNumber { get; set; }
        public string? DynamicsCode { get; set; }
        public DateTime? CreatedDate { get; set; }
        public bool? FatherAlive { get; set; }
        public string? FatherJob { get; set; }
        public bool? MotherAlive { get; set; }
        public string? MotherJob { get; set; }
        public string? DisabilityCause { get; set; }
        public string? TeacherLanguages { get; set; }
        public bool? IsFullCoverage { get; set; }
        public bool? IsOrphanStudent { get; set; }
        public string? Source { get; set; }
    }

    private sealed class ProjectItemDto
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public int TemplateId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string TitleAr { get; set; } = string.Empty;
        public string TitleEn { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string DescriptionAr { get; set; } = string.Empty;
        public string DescriptionEn { get; set; } = string.Empty;
        public int? ProjectTypeId { get; set; }
        public int? ProjectSubTypeId { get; set; }
        public string ProjectTypeName { get; set; } = string.Empty;
        public string ProjectTypeNameAr { get; set; } = string.Empty;
        public string ProjectTypeNameEn { get; set; } = string.Empty;
        public int? CountryId { get; set; }
        public string CountryAr { get; set; } = string.Empty;
        public string CountryEn { get; set; } = string.Empty;
        public decimal? Amount { get; set; }
        public string? AmountFormatted { get; set; }
        public decimal? PaidAmount { get; set; }
        public string? PaidAmountFormatted { get; set; }
        public decimal? RemainingAmount { get; set; }
        public string? RemainingAmountFormatted { get; set; }
        public string Image { get; set; } = string.Empty;
        public int? MainAccountTypeId { get; set; }
        public int? AccountTypeId { get; set; }
        public bool AcceptsSadaka { get; set; }
        public bool AcceptsZakaa { get; set; }
        public string Source { get; set; } = string.Empty;
    }

    private sealed class DonationItemDto
    {
        public int Id { get; set; }
        public int? PaidThroughId { get; set; }
        public int? PaidForId { get; set; }
        public int TypeId { get; set; }
        public int DonationsTypeId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string TitleAr { get; set; } = string.Empty;
        public string TitleEn { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string DescriptionAr { get; set; } = string.Empty;
        public string DescriptionEn { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string CategoryAr { get; set; } = string.Empty;
        public string CategoryEn { get; set; } = string.Empty;
        public int? CountryId { get; set; }
        public string CountryAr { get; set; } = string.Empty;
        public string CountryEn { get; set; } = string.Empty;
        public decimal? Amount { get; set; }
        public string? AmountFormatted { get; set; }
        public decimal? PaidAmount { get; set; }
        public string? PaidAmountFormatted { get; set; }
        public decimal? RemainingAmount { get; set; }
        public string? RemainingAmountFormatted { get; set; }
        public string Image { get; set; } = string.Empty;
        public int? MainAccountTypeId { get; set; }
        public int? AccountTypeId { get; set; }
        public int? ProjectTypeId { get; set; }
        public int? ProjectSubTypeId { get; set; }
        public int? NeedTypeId { get; set; }
        public int? DonorCount { get; set; }
        public bool AcceptsSadaka { get; set; }
        public bool AcceptsZakaa { get; set; }
        public string Source { get; set; } = string.Empty;
    }
}