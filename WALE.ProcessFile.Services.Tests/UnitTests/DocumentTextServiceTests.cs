using System.Text.Json;
using FakeItEasy;
using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.NoOcrService;
using WALE.ProcessFile.Services.PdfPig.Models;
using WALE.ProcessFile.Services.Services;
using Xunit;

namespace WALE.ProcessFile.Services.Tests.UnitTests;

/// Neither store holds text, so these fixtures feed the real serialised shapes.
public class DocumentTextServiceTests
{
    private static readonly Guid FileId = Guid.NewGuid();

    private readonly IDatabaseReadService _readService = A.Fake<IDatabaseReadService>();
    private readonly IDatabaseWriteService _writeService = A.Fake<IDatabaseWriteService>();

    private DocumentTextService Service() => new(_readService, _writeService);

    private static string NativePayload(params string[] lines) => NativeBlocks(lines);

    /// Baselines step by more than BlockTextHelper's 2pt tolerance, or two lines merge into one.
    private static string NativeBlocks(params string[][] blocks)
    {
        return JsonSerializer.Serialize(blocks
            .Select(lines => new MinimalTextBlock
            {
                TextLines = lines
                    .Select((line, index) => new MinimalTextLine
                    {
                        Words = Words(line, 800 - index * 20)
                    })
                    .ToList()
            })
            .ToList());
    }

    private static List<MinimalWord> Words(string line, double bottom)
    {
        var left = 0d;
        var words = new List<MinimalWord>();

        foreach (var word in line.Split(' '))
        {
            words.Add(new MinimalWord
            {
                Text = word,
                BoundingBox = new MinimalPdfRectangle
                {
                    Left = left,
                    Right = left + word.Length,
                    Bottom = bottom,
                    Top = bottom + 10
                }
            });

            left += word.Length + 5;
        }

        return words;
    }

    private static string OcrPayload(params string[] lines)
    {
        return JsonSerializer.Serialize(lines
            .Select(line => new LineAndWords
            {
                Words = line.Split(' ').Select(word => (DocumentLineWord?)new DocumentLineWord
                {
                    Text = word
                }).ToList()
            })
            .ToList());
    }

    private void NativeStoredAs(string? json)
    {
        A.CallTo(() => _readService.GetNoOcrAllPagesTextLinesAsync(
                A<NoOcrServiceMetadataCacheRequest>.That.Matches(request =>
                    request.FileId == FileId
                    && request.NoOcrServiceName == GeneralConstants.PdfPigDataExtractorServiceName)))
            .Returns(Task.FromResult<Dictionary<int, string>?>(json == null
                ? null
                : new Dictionary<int, string> { [1] = json }));
    }

    private void OcrStoredAs(params string[] pagePayloads)
    {
        A.CallTo(() => _readService.GetOcrCachedPagesAsync(FileId, A<string[]>._))
            .Returns(Task.FromResult(pagePayloads
                .Select((payload, index) => new OcrCachedPage
                {
                    OcrServiceName = "TesseractOcr-SparseTextOsd",
                    PageNumber = index + 1,
                    ImageNumber = 1,
                    Data = payload
                })
                .ToList()));
    }

    [Fact]
    public async Task RebuildsTextFromTheNativeDocumentLines()
    {
        NativeStoredAs(NativePayload(
            new string('x', 2048),
            "SCHEDULE OF CONDITIONS"));

        var text = await Service().GetDocumentTextAsync(FileId);

        Assert.Contains("SCHEDULE OF CONDITIONS", text);
        A.CallTo(() => _readService.GetOcrCachedPagesAsync(A<Guid>._, A<string[]>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task TwoColumnsAreReadColumnAtATimeRatherThanInterleaved()
    {
        NativeStoredAs(NativeBlocks(
            ["left one", "left two"],
            ["right one", "right two"]));

        OcrStoredAs();

        Assert.Equal(
            "left one\nleft two\nright one\nright two",
            await Service().GetDocumentTextAsync(FileId));
    }

    [Fact]
    public async Task RebuildsTextFromTheOcrPagesAndJoinsThemInOrder()
    {
        NativeStoredAs(null);
        OcrStoredAs(OcrPayload("first page text"), OcrPayload("second page text"));

        var text = await Service().GetDocumentTextAsync(FileId);

        Assert.Equal(
            "--- Page 1 ---\nfirst page text\n\n--- Page 2 ---\nsecond page text", text);
    }

    [Fact]
    public async Task AStubNativeLayerFallsThroughToOcr()
    {
        NativeStoredAs(NativePayload("x"));
        OcrStoredAs(OcrPayload("text from the OCR cache"));

        Assert.Equal("text from the OCR cache", await Service().GetDocumentTextAsync(FileId));
    }

    [Fact]
    public async Task AStubNativeLayerIsStillReturnedWhenNoOcrIsCached()
    {
        NativeStoredAs(NativePayload("a stub"));
        OcrStoredAs();

        Assert.Equal("a stub", await Service().GetDocumentTextAsync(FileId));
    }

    [Fact]
    public async Task NothingCachedYieldsNull()
    {
        NativeStoredAs(null);
        OcrStoredAs();

        Assert.Null(await Service().GetDocumentTextAsync(FileId));
    }

    [Fact]
    public async Task AnUnparseablePagePayloadIsSkippedRatherThanThrown()
    {
        NativeStoredAs(null);
        OcrStoredAs("""{"page":1,"angle":0.04,"width":2487}""", OcrPayload("the readable page"));

        Assert.Equal("the readable page", await Service().GetDocumentTextAsync(FileId));
    }

    [Fact]
    public async Task OnlyOcrServicesWhosePayloadCanBeParsedAreRequested()
    {
        NativeStoredAs(null);
        OcrStoredAs();

        await Service().GetDocumentTextAsync(FileId);

        A.CallTo(() => _readService.GetOcrCachedPagesAsync(
                FileId,
                A<string[]>.That.IsSameSequenceAs(
                    "AzureAiVisionOcr", "TesseractOcr-SparseTextOsd", "TesseractOcr-Auto")))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ASinglePageGetsNoPageBanner()
    {
        NativeStoredAs(null);
        OcrStoredAs(OcrPayload("the only page"));

        Assert.Equal("the only page", await Service().GetDocumentTextAsync(FileId));
    }

    [Fact]
    public async Task WhenEveryCachedPayloadIsUnreadable_ThenItThrowsRatherThanReturningNothing()
    {
        NativeStoredAs(null);
        OcrStoredAs("""{"not":"a known shape"}""", """{"also":"unreadable"}""");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Service().GetDocumentTextAsync(FileId));

        Assert.Contains(FileId.ToString(), exception.Message);
    }

    [Fact]
    public async Task NothingCachedStillYieldsNullRatherThanThrowing()
    {
        NativeStoredAs(null);
        OcrStoredAs();

        Assert.Null(await Service().GetDocumentTextAsync(FileId));
    }

    private void StoredAs(StoredDocumentText? stored)
    {
        A.CallTo(() => _readService.GetStoredDocumentTextAsync(FileId))
            .Returns(Task.FromResult(stored));
    }

    [Fact]
    public async Task AStoredRowAtTheCurrentFormatIsReturnedWithoutDeriving()
    {
        StoredAs(new StoredDocumentText
        {
            SourceService = "AzureAiVisionOcr",
            FormatVersion = DocumentTextService.CurrentFormatVersion,
            Text = "text from the store"
        });

        Assert.Equal("text from the store", await Service().GetDocumentTextAsync(FileId));

        A.CallTo(() => _readService.GetNoOcrAllPagesTextLinesAsync(
            A<NoOcrServiceMetadataCacheRequest>._)).MustNotHaveHappened();
        A.CallTo(() => _readService.GetOcrCachedPagesAsync(A<Guid>._, A<string[]>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task AStoredRowAtAnOlderFormatIsIgnoredAndDerivedAgain()
    {
        StoredAs(new StoredDocumentText
        {
            SourceService = "TesseractOcr-SparseTextOsd",
            FormatVersion = DocumentTextService.CurrentFormatVersion - 1,
            Text = "text in the old format"
        });

        NativeStoredAs(null);
        OcrStoredAs(OcrPayload("freshly derived text"));

        Assert.Equal("freshly derived text", await Service().GetDocumentTextAsync(FileId));
    }

    [Fact]
    public async Task ReadingNeverStores()
    {
        StoredAs(null);
        NativeStoredAs(NativePayload(new string('x', 2048), "native text"));

        await Service().GetDocumentTextAsync(FileId);

        A.CallTo(() => _writeService.SaveDocumentTextAsync(
                A<Guid>._, A<string>._, A<int>._, A<string>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task StoringRecordsTheExtractorThatProducedTheText()
    {
        NativeStoredAs(NativePayload(new string('x', 2048), "native text"));

        await Service().StoreDocumentTextAsync(FileId);

        A.CallTo(() => _writeService.SaveDocumentTextAsync(
                FileId,
                GeneralConstants.PdfPigDataExtractorServiceName,
                DocumentTextService.CurrentFormatVersion,
                A<string>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task AFileWithNothingCachedIsNotStored()
    {
        NativeStoredAs(null);
        OcrStoredAs();

        await Service().StoreDocumentTextAsync(FileId);

        A.CallTo(() => _writeService.SaveDocumentTextAsync(
                A<Guid>._, A<string>._, A<int>._, A<string>._))
            .MustNotHaveHappened();
    }
}
