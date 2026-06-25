using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TalaPress.Pages;

public class WorkflowModel : PageModel
{
    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return RedirectToPage("/Login");
        }

        if (!User.HasClaim("Permission", "Settings.Edit"))
        {
            TempData["ErrorMessage"] = "ليس لديك صلاحية كافية لعرض صفحة مسارات العمل.";
            return RedirectToPage("/Index");
        }

        return Page();
    }
}
