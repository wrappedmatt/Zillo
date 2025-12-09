using Microsoft.AspNetCore.Mvc;

namespace Zillo.API.Controllers;

[ApiController]
[Route("api")]
public class HealthController : ControllerBase
{
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { status = "healthy", service = "api", version = "1.0.0", timestamp = DateTime.UtcNow });
    }
}
