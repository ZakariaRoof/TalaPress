using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TalaPress.Pages.ApiDocs;

public abstract class ApiDocsPageModel : PageModel
{
    public string BaseUrl { get; private set; } = string.Empty;

    public string ApiV1BaseUrl => $"{BaseUrl}/api/v1";

    protected void PopulateBaseUrl()
    {
        BaseUrl = $"{Request.Scheme}://{Request.Host}";
    }

    protected IActionResult? RequireApiDocsAccess()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return RedirectToPage("/Login");
        }

        if (!CanViewApiDocs())
        {
            return RedirectToPage("/Login");
        }

        return null;
    }

    protected bool CanViewApiDocs()
    {
        return User.Claims.Any(c => c.Type == "Permission");
    }
}
