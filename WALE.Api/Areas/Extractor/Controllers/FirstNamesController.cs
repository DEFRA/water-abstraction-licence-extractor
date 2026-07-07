using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using WALE.ProcessFile.Core.Helpers;

namespace WALE.Api.Areas.Extractor.Controllers;

[ApiController]
[Area("Extractor")]
[Route("/[area]/[controller]/[action]")]
public class FirstNamesController : Controller
{
    [OutputCache(Duration=60)] // Doesn't change often at all
    [HttpGet]
    public async Task<IActionResult> GetAllAsync()
    {
        var firstNames = await CompanyNameHelper.GetFirstNamesCsvFromFileAsync();
        return Ok(firstNames);
    }
}