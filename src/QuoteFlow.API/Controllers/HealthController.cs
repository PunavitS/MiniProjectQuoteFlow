using Microsoft.AspNetCore.Mvc;

namespace QuoteFlow.API.Controllers;

[ApiController]
[Route("health")]
[Tags("Health")]
public class HealthController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetHealth()
    {
        return Ok(new
        {
            status = "healthy",
            timestamp = DateTimeOffset.UtcNow
        });
    }
}
