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

    private readonly IConfiguration _configuration;
    private readonly ILogger<SponsorshipsController> _logger;

    public SponsorshipsController(IConfiguration configuration, ILogger<SponsorshipsController> logger)
    {
        _configuration = configuration;
        _logger = logger;
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
        [FromQuery] int countryId = QatarCountryId,
        [FromQuery] int sponsorshipCategoryId = 0,
        [FromQuery] string? searchQuery = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 12,
        [FromQuery] string? sortBy = "newest",
        [FromQuery(Name = "mostwaiting")] bool mostWaiting = true,
        [FromQuery] bool birthday = false,
        CancellationToken cancellationToken = default)
    {
        if (typeId != 3)
        {
            return BadRequest(new { message = "Only sponsorship donation items are available from this endpoint in TalaPress." });
        }

        if (countryId != QatarCountryId)
        {
            return BadRequest(new { message = "Alakraboon sponsorships are limited to Qatar countryId 542." });
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

    private static int NormalizeLanguageId(int languageId) => languageId == 2 ? 2 : 1;

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
}