using Microsoft.AspNetCore.Mvc;

namespace TalaPress.Pages.ApiDocs;

public class ContentTypesModel : ApiDocsPageModel
{
    public IActionResult OnGet()
    {
        IActionResult? denied = RequireApiDocsAccess();
        if (denied != null)
        {
            return denied;
        }

        PopulateBaseUrl();
        ViewData["Title"] = "Content Types";
        ViewData["ApiDocsSection"] = "content-types";
        return Page();
    }
}
