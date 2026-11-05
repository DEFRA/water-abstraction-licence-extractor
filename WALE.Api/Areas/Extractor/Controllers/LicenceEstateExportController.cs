using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WALE.Api.Interfaces;
using WRADI.Core.AbstractionLicence.Interfaces;
using WRADI.Core.AbstractionLicence.Models;

namespace WALE.Api.Areas.Extractor.Controllers;

[ApiController]
[Area("Extractor")]
[Route("/[area]/[controller]/[action]")]
public class LicenceEstateExportController(
    ILicenceEstateExportService licenceEstateExportService,
    ILicenceEstateExportRunner licenceEstateExportRunner) : Controller
{
    [HttpGet]
    public Task<IActionResult> GetLicenceAsync([FromQuery] int licenceId) =>
        RespondAsync(licenceEstateExportService.ExportLicenceAsync(licenceId));

    [HttpGet]
    public Task<IActionResult> GetLicenceByNumberAsync(
        [FromQuery] string licenceNumber,
        [FromQuery] int processRunId) =>
        RespondAsync(licenceEstateExportService.ExportLicenceAsync(licenceNumber, processRunId));

    private async Task<IActionResult> RespondAsync(Task<LicenceExport?> exporting)
    {
        var licence = await exporting;

        if (licence == null)
        {
            return NotFound();
        }

        // Serialised here rather than returned as an object: the action's own options would
        // put back the NoneSchemaData that LicenceExportSerializer exists to drop.
        return Content(
            JsonSerializer.Serialize(licence, LicenceExportSerializer.Options),
            "application/json");
    }

    /// A run takes minutes: the zip arrives on the process-{processRunId} SignalR group,
    /// not in this response.
    [HttpPost]
    public IActionResult StartRunExport([FromQuery] int processRunId)
    {
        if (processRunId == 0)
        {
            return BadRequest();
        }

        licenceEstateExportRunner.StartRunExport(processRunId);

        return Accepted();
    }

    [HttpGet]
    public async Task<IActionResult> GetRunExportUrlAsync([FromQuery] int processRunId)
    {
        var url = await licenceEstateExportRunner.GetRunExportUrlAsync(processRunId);

        return url == null ? NotFound() : Ok(new { url });
    }
}
