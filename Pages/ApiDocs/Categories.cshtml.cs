using Microsoft.AspNetCore.Mvc;

namespace TalaPress.Pages.ApiDocs;

public class CategoriesModel : ApiDocsPageModel
{
    public IActionResult OnGet()
    {
        IActionResult? denied = RequireApiDocsAccess();
        if (denied != null)
        {
            return denied;
        }

        PopulateBaseUrl();
        ViewData["Title"] = "Categories";
        ViewData["ApiDocsSection"] = "categories";
        return Page();
    }
}
