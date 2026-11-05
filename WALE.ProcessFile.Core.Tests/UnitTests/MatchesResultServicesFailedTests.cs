using System.Text.Json;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OcrService;

namespace WALE.ProcessFile.Core.Tests.UnitTests;

/// <summary>
/// matches_result.data is the serialised <see cref="MatchesResult"/>. These pin the contract that
/// OCR provider failures survive the round trip, and that rows written before the field existed
/// still deserialise (as "no failures recorded").
/// </summary>
public class MatchesResultServicesFailedTests
{
    [Fact]
    public void WhenServicesFailedRecorded_ThenRoundTripsThroughJson()
    {
        // Arrange
        var result = new MatchesResult
        {
            Filename = "file.pdf",
            ServicesUsed = ["TesseractOcr-SparseTextOsd"],
            ServicesFailed =
            [
                new OcrServiceFailure
                {
                    ServiceName = "AwsTextractOcrDataExtractorService",
                    PageNumber = 3,
                    ImageNumber = 1,
                    IsTransient = true,
                    ErrorType = "ProvisionedThroughputExceededException",
                    Message = "The number of requests exceeded your throughput limit."
                }
            ]
        };

        // Act
        var json = JsonSerializer.Serialize(result, JsonHelper.GetSerializerOptions());
        var roundTripped = JsonSerializer.Deserialize<MatchesResult>(json, JsonHelper.GetSerializerOptions());

        // Assert
        Assert.NotNull(roundTripped);
        Assert.DoesNotContain("AwsTextractOcrDataExtractorService", roundTripped.ServicesUsed);

        var failure = Assert.Single(roundTripped.ServicesFailed);
        Assert.Equal("AwsTextractOcrDataExtractorService", failure.ServiceName);
        Assert.Equal(3, failure.PageNumber);
        Assert.Equal(1, failure.ImageNumber);
        Assert.True(failure.IsTransient);
        Assert.Equal("ProvisionedThroughputExceededException", failure.ErrorType);
    }

    [Fact]
    public void WhenJsonPredatesServicesFailed_ThenDeserialisesWithNoFailures()
    {
        // Arrange - shape of a row saved before this field existed
        const string legacyJson =
            """{"filename":"file.pdf","matchesResultId":1,"fileId":"00000000-0000-0000-0000-000000000001","status":"Ok","regionCode":1,"numberOfPages":2,"scannedFile":true,"servicesUsed":["PdfPig","AwsTextractOcrDataExtractorService"],"pages":[]}""";

        // Act
        var result = JsonSerializer.Deserialize<MatchesResult>(legacyJson, JsonHelper.GetSerializerOptions());

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.ServicesFailed);
        Assert.Equal(2, result.ServicesUsed.Count);
    }
}
