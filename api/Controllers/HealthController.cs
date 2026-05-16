using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("")]
public class HealthController : ControllerBase
{
    [HttpGet("health/live")]
    public IActionResult Live() => Ok(new { status = "alive" });
}
