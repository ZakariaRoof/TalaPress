using Microsoft.AspNetCore.Mvc;

namespace TalaPress.Pages.ApiDocs;

public class IndexModel : ApiDocsPageModel
{
    public IActionResult OnGet()
    {
        IActionResult? denied = RequireApiDocsAccess();
        if (denied != null)
        {
            return denied;
        }

        PopulateBaseUrl();
        ViewData["Title"] = "Overview";
        ViewData["ApiDocsSection"] = "overview";
        return Page();
    }
}
