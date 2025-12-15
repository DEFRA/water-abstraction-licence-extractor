using Microsoft.AspNetCore.Mvc;

namespace WaterAbstractionLicenseExtractor.Api.Controllers;

[ApiController]
public class PlaceholderController : Controller
{
    [HttpGet("/placeholder")]
    public IActionResult Index()
    {
        return Ok("Hello World!");
    }
}