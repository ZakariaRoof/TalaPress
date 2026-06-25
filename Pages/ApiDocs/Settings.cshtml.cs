using Microsoft.AspNetCore.Mvc;

namespace TalaPress.Pages.ApiDocs;

public class SettingsModel : ApiDocsPageModel
{
    public IActionResult OnGet()
    {
        IActionResult? denied = RequireApiDocsAccess();
        if (denied != null)
        {
            return denied;
        }

        PopulateBaseUrl();
        ViewData["Title"] = "Settings";
        ViewData["ApiDocsSection"] = "settings";
        return Page();
    }
}
