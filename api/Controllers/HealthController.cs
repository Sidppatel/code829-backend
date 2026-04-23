using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("")]
public class HealthController : ControllerBase
{
    /// <summary>
    /// Liveness probe — returns 200 if the process is running.
    /// Used by load balancers to detect crashed instances.
    /// </summary>
    [HttpGet("health/live")]
    public IActionResult Live() => Ok(new { status = "alive" });
}
