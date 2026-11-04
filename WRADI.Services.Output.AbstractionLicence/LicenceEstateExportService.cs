using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Interfaces;
using WRADI.Core.AbstractionLicence.Helpers;
using WRADI.Core.AbstractionLicence.Interfaces;
using WRADI.Core.AbstractionLicence.Models;

namespace WRADI.Services.Output.AbstractionLicence;

public class LicenceEstateExportService(
    IAbstractionLicenceOutputService abstractionLicenceOutputService,
    IAbstractionLicenceDatabaseReadService abstractionLicenceDatabaseReadService,
    IDocumentTextService documentTextService) : ILicenceEstateExportService
{

    public async Task<LicenceExport?> ExportLicenceAsync(int licenceId)
    {
        // Applied rather than filtered: who decided each one is recorded per entry instead.
        var licence = await abstractionLicenceOutputService.GetLicenceAsync(
            licenceId,
            applyVerifications: true);

        if (licence == null)
        {
            return null;
        }

        var documentText = licence.DmsFileId == null
            ? null
            : await documentTextService.GetDocumentTextAsync(licence.DmsFileId.Value);

        return new LicenceExport
        {
            LicenceId = licenceId,
            ProcessRunId = licence.ProcessRunId,
            MatchesResultId = licence.MatchesResultId,
            Status = licence.Status,
            LicenceNumber = licence.LicenceNumber,
            RegionId = licence.RegionId,
            LicenceType = licence.LicenceType,
            DmsPermitNumber = licence.DmsPermitNumber,
            DmsPath = licence.DmsPath,
            DmsFileId = licence.DmsFileId,
            NaldStatus = licence.NaldStatus,
            NaldHasAggregateCondition = licence.NaldHasAggregateCondition,
            Filename = licence.Filename,
            TemplateType = licence.TemplateType,
            Template = licence.Template,
            LicenceVersion = licence.LicenceVersion,
            Points = licence.Points,
            Purposes = licence.Purposes,
            PeriodsOfAbstraction = licence.PeriodsOfAbstraction,
            MeansOfAbstraction = licence.MeansOfAbstraction,
            AbstractionLimits = licence.AbstractionLimits,
            DefinitionOfYear = licence.DefinitionOfYear,
            LinkedLicences = licence.LinkedLicences,
            LicenceSets = licence.LicenceSets,

            IssuedTo = IssuedTo(licence),
            DocumentText = Pages(documentText),
            Verifications = await VerificationsAsync(licence)
        };
    }

    public async Task<LicenceEstateExportSummary> ExportRunAsync(int processRunId, Stream destination)
    {
        const int pageSize = 500;

        using var zip = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true);

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var written = 0;
        var notExtracted = 0;
        var unreadable = 0;
        var withoutDocumentText = 0;
        var withoutTemplateType = 0;

        for (var skip = 0; ; skip += pageSize)
        {
            var page = await abstractionLicenceOutputService.GetLicencesAsync(
                processRunId,
                skip,
                pageSize);

            if (page.Count == 0)
            {
                break;
            }

            foreach (var listed in page)
            {
                // A run holds a row per licence it looked for, not per licence it read, and a
                // row it never read can carry no file id or process run id - which
                // DatabaseAbstractionLicenceOutputService force-unwraps when applying verifications.
                if (listed.Status != ScrapeStatus.Ok)
                {
                    notExtracted++;
                    continue;
                }

                if (listed.NoneSchemaData.GetValueOrDefault("licenceId") is not { } rawId
                    || !int.TryParse(rawId.ToString(), out var licenceId))
                {
                    unreadable++;
                    continue;
                }

                var licence = await ExportLicenceAsync(licenceId);

                if (licence?.LicenceNumber?.Value == null)
                {
                    unreadable++;
                    continue;
                }

                var name = FileName(licence.LicenceNumber.Value);

                if (!names.Add(name))
                {
                    throw new InvalidOperationException(
                        $"Two licences both export as {name} - the second is "
                        + $"{licence.LicenceNumber.Value}.");
                }

                if (licence.DocumentText == null)
                {
                    withoutDocumentText++;
                }

                if (string.IsNullOrEmpty(licence.TemplateType))
                {
                    withoutTemplateType++;
                }

                await using var entry = zip.CreateEntry(name).Open();
                await JsonSerializer.SerializeAsync(entry, licence, LicenceExportSerializer.Options);

                written++;
            }

            if (page.Count < pageSize)
            {
                break;
            }
        }

        return new LicenceEstateExportSummary(
            written,
            notExtracted,
            unreadable,
            withoutDocumentText,
            withoutTemplateType);
    }

    private static string FileName(string licenceNumber) =>
        new string(licenceNumber.Where(char.IsLetterOrDigit).ToArray()) + ".json";

    private static ValueWithConfidence<string>? IssuedTo(Licence licence)
    {
        if (licence.NoneSchemaData.GetValueOrDefault("issuedTo")?.ToString() is not { } issuedTo
            || string.IsNullOrWhiteSpace(issuedTo))
        {
            return null;
        }

        return new ValueWithConfidence<string>(
            issuedTo,
            null,
            double.TryParse(
                licence.NoneSchemaData.GetValueOrDefault("issuedToConfidence")?.ToString(),
                out var confidence)
                ? confidence
                : null);
    }

    private async Task<LicenceVerification[]> VerificationsAsync(Licence licence)
    {
        if (licence.DmsFileId == null || licence.LicenceNumber?.Value == null)
        {
            return [];
        }

        var verifications = await abstractionLicenceDatabaseReadService
            .GetLicenceSectionVerificationsAsync(
                licence.DmsFileId.Value,
                licence.LicenceNumber.Value);

        // The read returns withdrawn verifications too.
        return verifications
            .Where(verification => verification.DeletedDateTimeUtc == null)
            .Select(verification => new LicenceVerification
            {
                Section = verification.LicenceSectionName,
                Item = verification.LicenceSectionItemId,
                VerificationType = verification.VerificationType,
                VerifiedBy = VerificationProvenance.For(verification.VerificationType),
                ScrapedValue = Node(verification.LicenceSectionScrapedValue),
                AppliedValue = Node(verification.LicenceSectionOverrideValue),
                Notes = verification.Notes
            })
            .ToArray();
    }

    private static DocumentTextPage[]? Pages(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var parts = text.Split("--- Page ");

        if (parts.Length == 1)
        {
            return [new DocumentTextPage { PageNumber = 1, Text = text.Trim() }];
        }

        var pages = new List<DocumentTextPage>();

        foreach (var part in parts.Skip(1))
        {
            var banner = part.IndexOf(" ---", StringComparison.Ordinal);

            if (banner < 0 || !int.TryParse(part[..banner], out var pageNumber))
            {
                continue;
            }

            pages.Add(new DocumentTextPage
            {
                PageNumber = pageNumber,
                Text = part[(banner + 4)..].Trim()
            });
        }

        return pages.ToArray();
    }

    private static JsonNode? Node(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(json);
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }
}
