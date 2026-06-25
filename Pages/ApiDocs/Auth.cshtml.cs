using Microsoft.AspNetCore.Mvc;

namespace TalaPress.Pages.ApiDocs;

public class AuthModel : ApiDocsPageModel
{
    public IActionResult OnGet()
    {
        IActionResult? denied = RequireApiDocsAccess();
        if (denied != null)
        {
            return denied;
        }

        PopulateBaseUrl();
        ViewData["Title"] = "Authentication";
        ViewData["ApiDocsSection"] = "auth";
        return Page();
    }
}
