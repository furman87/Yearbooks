using Microsoft.AspNetCore.Mvc;

namespace YearbookViewer.Controllers;

[ApiController]
[Route("[controller]")]
public class HealthController : ControllerBase
{
    private readonly ILogger<HealthController> _logger;

    public HealthController(ILogger<HealthController> logger)
    {
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new { 
            status = "healthy",
            timestamp = DateTime.UtcNow,
            service = "Yearbook Viewer"
        });
    }
}