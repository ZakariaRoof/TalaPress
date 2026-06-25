using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Data.SqlClient;

namespace TalaPress.Pages;

public class DashboardPostViewModel {
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? PublishDate { get; set; }
    public string ContentTypeName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
}

public class DashboardContentTypeStat {
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class DashboardMonthlyStat {
    public int Month { get; set; }
    public int Count { get; set; }
}

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly IConfiguration _configuration;

    public int TotalPosts { get; set; }
    public int TotalViews { get; set; }
    public int PendingCount { get; set; }
    public int ScheduledCount { get; set; }

    public List<DashboardPostViewModel> RecentPostsAll { get; set; } = new();
    public List<DashboardPostViewModel> RecentPostsToday { get; set; } = new();
    public List<DashboardPostViewModel> RecentPostsWeek { get; set; } = new();
    public List<DashboardPostViewModel> RecentPostsMonth { get; set; } = new();

    public List<DashboardPostViewModel> PendingPosts { get; set; } = new();
    public List<DashboardPostViewModel> ScheduledPosts { get; set; } = new();

    public List<DashboardContentTypeStat> ContentTypeStats { get; set; } = new();
    public List<DashboardMonthlyStat> MonthlyStats { get; set; } = new();

    public IndexModel(ILogger<IndexModel> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public string CurrentPeriod { get; set; } = "all";

    public async Task<IActionResult> OnGetAsync(string period = "all")
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return RedirectToPage("/Login");
        }

        // Check if user has Dashboard.View. If not, redirect to the first available page they have access to.
        if (!User.HasClaim("Permission", "Dashboard.View"))
        {
            if (User.HasClaim("Permission", "Content.View"))
            {
                return RedirectToPage("/Forms");
            }
            if (User.HasClaim("Permission", "Category.View"))
            {
                return RedirectToPage("/Categories");
            }
            if (User.HasClaim("Permission", "Roles.View") || User.HasClaim("Permission", "Permissions.View"))
            {
                return RedirectToPage("/Permissions");
            }
            if (User.HasClaim("Permission", "Users.View"))
            {
                return RedirectToPage("/Users");
            }

            // No permissions at all: Sign out and redirect to login
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            TempData["ErrorMessage"] = "هذا الحساب لا يملك أي صلاحيات وصول للوحة التحكم.";
            return RedirectToPage("/Login");
        }

        CurrentPeriod = period;
        await LoadDashboardDataAsync(period);

        return Page();
    }

    private async Task LoadDashboardDataAsync(string period)
    {
        string connectionString = _configuration.GetConnectionString("DefaultConnection") ?? "";
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        var today = DateTime.UtcNow.Date;
        var startOfWeek = today.AddDays(-(int)today.DayOfWeek);
        var startOfMonth = new DateTime(today.Year, today.Month, 1);

        // 1. Load Stats
        using (var cmd = new SqlCommand(@"
            SELECT 
                (SELECT COUNT(*) FROM dbo.Content WHERE IsDeleted = 0) AS TotalPosts,
                (SELECT ISNULL(SUM(Hits), 0) FROM dbo.Content WHERE IsDeleted = 0) AS TotalViews,
                (SELECT COUNT(*) FROM dbo.Content WHERE IsDeleted = 0 AND Status = 'Pending') AS PendingCount,
                (SELECT COUNT(*) FROM dbo.Content WHERE IsDeleted = 0 AND Status = 'Scheduled') AS ScheduledCount
        ", connection))
        {
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                TotalPosts = reader.GetInt32(0);
                TotalViews = reader.GetInt32(1);
                PendingCount = reader.GetInt32(2);
                ScheduledCount = reader.GetInt32(3);
            }
        }

        // 2. Load Content Type Stats
        string dateFilter = "";
        if (period == "today") dateFilter = "AND c.CreatedAt >= CAST(GETUTCDATE() AS DATE)";
        else if (period == "week") dateFilter = "AND c.CreatedAt >= DATEADD(day, -7, GETUTCDATE())";
        else if (period == "month") dateFilter = "AND c.CreatedAt >= DATEADD(month, -1, GETUTCDATE())";
        else if (period == "year") dateFilter = "AND c.CreatedAt >= DATEADD(year, -1, GETUTCDATE())";

        using (var cmd = new SqlCommand($@"
            SELECT ct.Name, COUNT(c.Id) 
            FROM dbo.ContentTypes ct 
            LEFT JOIN dbo.Content c ON ct.Id = c.ContentTypeId AND c.IsDeleted = 0 {dateFilter}
            GROUP BY ct.Name
        ", connection))
        {
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                ContentTypeStats.Add(new DashboardContentTypeStat {
                    Name = reader.GetString(0),
                    Count = reader.GetInt32(1)
                });
            }
        }

        // 3. Load Monthly Stats (Current Year)
        using (var cmd = new SqlCommand(@"
            SELECT MONTH(CreatedAt) AS MonthNumber, COUNT(*) AS PostsCount
            FROM dbo.Content
            WHERE IsDeleted = 0 AND YEAR(CreatedAt) = YEAR(GETUTCDATE())
            GROUP BY MONTH(CreatedAt)
            ORDER BY MonthNumber
        ", connection))
        {
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                MonthlyStats.Add(new DashboardMonthlyStat {
                    Month = reader.GetInt32(0),
                    Count = reader.GetInt32(1)
                });
            }
        }

        // 4. Load Posts (Recent, Pending, Scheduled)
        var allPosts = new List<DashboardPostViewModel>();
        using (var cmd = new SqlCommand(@"
            SELECT TOP 200 c.Id, c.Title, c.Status, c.CreatedAt, c.PublishDate, ct.Name AS ContentTypeName, cat.Name AS CategoryName
            FROM dbo.Content c
            LEFT JOIN dbo.ContentTypes ct ON c.ContentTypeId = ct.Id
            LEFT JOIN dbo.Categories cat ON c.CategoryId = cat.Id
            WHERE c.IsDeleted = 0
            ORDER BY c.CreatedAt DESC
        ", connection))
        {
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                allPosts.Add(new DashboardPostViewModel {
                    Id = reader.GetInt64(0),
                    Title = reader.IsDBNull(1) ? "بدون عنوان" : reader.GetString(1),
                    Status = reader.IsDBNull(2) ? "Draft" : reader.GetString(2),
                    CreatedAt = reader.GetDateTime(3),
                    PublishDate = reader.IsDBNull(4) ? null : reader.GetDateTime(4),
                    ContentTypeName = reader.IsDBNull(5) ? "غير محدد" : reader.GetString(5),
                    CategoryName = reader.IsDBNull(6) ? "غير محدد" : reader.GetString(6)
                });
            }
        }

        // Distribute to view models
        PendingPosts = allPosts.Where(p => p.Status == "Pending").Take(5).ToList();
        ScheduledPosts = allPosts.Where(p => p.Status == "Scheduled").Take(5).ToList();
        
        RecentPostsAll = allPosts.Take(5).ToList();
        RecentPostsToday = allPosts.Where(p => p.CreatedAt.Date == today).Take(5).ToList();
        RecentPostsWeek = allPosts.Where(p => p.CreatedAt.Date >= startOfWeek).Take(5).ToList();
        RecentPostsMonth = allPosts.Where(p => p.CreatedAt.Date >= startOfMonth).Take(5).ToList();
    }
}
