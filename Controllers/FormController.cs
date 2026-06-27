using Microsoft.AspNetCore.Mvc;
using TalaPress.Services;

namespace TalaPress.Controllers;

[Route("api/forms")]
[ApiController]
[Obsolete("Use POST /api/v1/forms/{id}/submit with Pearl API key.")]
public class FormController : ControllerBase
{
    [HttpPost("submit")]
    [IgnoreAntiforgeryToken]
    public IActionResult SubmitForm()
    {
        return StatusCode(410, new
        {
            success = false,
            message = "This endpoint is deprecated. Use POST /api/v1/forms/{formId}/submit with Pearl API key in Authorization or X-Pearl-Key header."
        });
    }
}
