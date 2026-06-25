using Microsoft.AspNetCore.Mvc;

namespace TalaPress.Pages.ApiDocs;

public class ContentModel : ApiDocsPageModel
{
    public IActionResult OnGet()
    {
        IActionResult? denied = RequireApiDocsAccess();
        if (denied != null)
        {
            return denied;
        }

        PopulateBaseUrl();
        ViewData["Title"] = "Content";
        ViewData["ApiDocsSection"] = "content";
        return Page();
    }
}
