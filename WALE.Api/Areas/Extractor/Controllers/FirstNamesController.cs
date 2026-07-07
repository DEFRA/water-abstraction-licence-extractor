using Microsoft.AspNetCore.Mvc;
using WALE.ProcessFile.Core.Helpers;

namespace WALE.Api.Areas.Extractor.Controllers;

[ApiController]
[Area("Extractor")]
[Route("/[area]/[controller]/[action]")]
public class FirstNamesController : Controller
{
    [HttpGet]
    public async Task<IActionResult> GetAllAsync()
    {
        var firstNames = await CompanyNameHelper.GetFirstNamesCsvFromFileAsync();
        return Ok(firstNames);
    }
}