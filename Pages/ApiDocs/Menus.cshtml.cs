using Microsoft.AspNetCore.Mvc;

namespace TalaPress.Pages.ApiDocs;

public class MenusModel : ApiDocsPageModel
{
    public IActionResult OnGet()
    {
        IActionResult? denied = RequireApiDocsAccess();
        if (denied != null)
        {
            return denied;
        }

        PopulateBaseUrl();
        ViewData["Title"] = "Menus";
        ViewData["ApiDocsSection"] = "menus";
        return Page();
    }
}
