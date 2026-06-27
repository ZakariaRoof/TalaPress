using Microsoft.AspNetCore.Mvc;

namespace TalaPress.Pages.ApiDocs;

public class FormsModel : ApiDocsPageModel
{
    public IActionResult OnGet()
    {
        IActionResult? denied = RequireApiDocsAccess();
        if (denied != null)
        {
            return denied;
        }

        PopulateBaseUrl();
        ViewData["Title"] = "Forms";
        ViewData["ApiDocsSection"] = "forms";
        return Page();
    }
}
