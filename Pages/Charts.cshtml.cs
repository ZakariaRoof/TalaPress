using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TalaPress.Pages;

public class ChartsModel : PageModel
{
    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return RedirectToPage("/Login");
        }

        if (!User.HasClaim("Permission", "Dashboard.View"))
        {
            TempData["ErrorMessage"] = "ليس لديك صلاحية كافية لعرض لوحة الإحصائيات والرسوم.";
            return RedirectToPage("/Index");
        }

        return Page();
    }
}
