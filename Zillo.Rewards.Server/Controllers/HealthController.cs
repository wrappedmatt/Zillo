using Microsoft.AspNetCore.Mvc;

namespace Zillo.Rewards.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new { status = "healthy", service = "rewards", version = "1.0.0" });
    }
}
