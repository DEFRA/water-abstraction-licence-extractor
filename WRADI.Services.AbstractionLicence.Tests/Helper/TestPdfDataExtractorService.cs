using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.Dms;
using WRADI.Core.AbstractionLicence.Models;
using WRADI.Services.AbstractionLicence.Tests.UnitTests;

namespace WRADI.Services.AbstractionLicence.Tests.Helper;

public class TestPdfDataExtractorService(Dictionary<string, MatchesResult> data) : IPdfDataExtractorService
{
    public int Id { get; set; }
    public bool InUse { get; set; }

    public Task<(bool StopExecution, bool? AlreadySaved, MatchesResult? Item)> GetMatchesAsync(
        string pdfFileName,
        DmsFileData dmsDataForFile,
        LookupConfiguration configuration,
        List<string> previouslyParsedFiles,
        int processRunId)
    {
        if (!data.TryGetValue(pdfFileName, out var value))
        {
            var matchesResult = new MatchesResult
            {
                Status = nameof(ScrapeStatus.Ok),
                Filename = pdfFileName,
                Matches = [
                    new()
                    {
                        LabelGroupName = "LicenceNumber",
                        MatchedLabelName = "DocumentLicenceNumber",
                        Text = PdfPigNoOcrPdfTests.TextToLines(pdfFileName)
                    }
                ]
            };
            
            var errorReturnItem = (false, (bool?)true, (MatchesResult?)matchesResult);
            return Task.FromResult(errorReturnItem);
        }
        
        var returnItem = (false, (bool?)true, (MatchesResult?)value);
        return Task.FromResult(returnItem);
    }

    public Task SaveMatchResultAsync(
        MatchesResult matchesResult,
        Guid fileId,
        int processRunId,
        bool isUpdate)
    {
        return Task.CompletedTask;
    }

    public Task<List<LabelGroupResult>> ProcessSubLabelsAsync(
        LabelToMatch label,
        IReadOnlyList<DocumentLine> text,
        bool isOcr,
        string? serviceName,
        string labelGroupName,
        List<string> previouslyParsedPaths,
        int regionCode,
        int processRunId,
        LookupConfiguration configuration,
        IDocumentLineService documentLineService,
        Dictionary<string, object?> additionalInformationStore)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }
}