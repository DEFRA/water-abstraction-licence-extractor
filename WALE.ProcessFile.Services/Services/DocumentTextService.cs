using System.Text;
using System.Text.Json;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Services.Services;

public class DocumentTextService(IDatabaseReadService databaseReadService) : IDocumentTextService
{
    /// A document's native text is a full layer or none at all - a median 15,000 characters
    /// against 0 - so any threshold inside that gap behaves the same.
    private const int SmallestUsefulNativeText = 2048;

    private const string AzureOcrServiceName = "AzureAiVisionOcr";

    /// Ordered best-first: SparseTextOsd reads a form's ruled lines as text, Azure does not.
    /// An allow-list - TabulaSharp here is table extraction, and Textract caches one word per entry.
    private static readonly string[] OcrServicesByPreference =
    [
        AzureOcrServiceName,
        "TesseractOcr-SparseTextOsd",
        "TesseractOcr-Auto"
    ];

    private static readonly JsonSerializerOptions SerializerOptions =
        new() { PropertyNameCaseInsensitive = true };

    public async Task<string?> GetDocumentTextAsync(Guid fileId)
    {
        var unreadable = new List<string>();

        var nativeText = await ReadNativeTextAsync(fileId, unreadable);

        if (nativeText?.Length >= SmallestUsefulNativeText)
        {
            return nativeText;
        }

        var ocrText = await ReadOcrTextAsync(fileId, unreadable);
        var text = string.IsNullOrWhiteSpace(ocrText) ? nativeText : ocrText;

        // Nothing cached is legitimately empty; payloads that all fail to parse are a defect,
        // and returning null for those hides it as an ordinary 204.
        if (string.IsNullOrWhiteSpace(text) && unreadable.Count > 0)
        {
            throw new InvalidOperationException(
                $"Cached text exists for {fileId} but none of it could be read: " +
                $"{string.Join("; ", unreadable)}.");
        }

        return text;
    }

    private async Task<string?> ReadNativeTextAsync(Guid fileId, List<string> unreadable)
    {
        var stored = await databaseReadService.GetAllPagesTextAsync(
            fileId,
            GeneralConstants.PdfPigDataExtractorServiceName);

        if (string.IsNullOrWhiteSpace(stored))
        {
            return null;
        }

        var lines = Deserialize<List<DocumentLine>>(stored, unreadable, "all_pages_text");

        return lines == null
            ? null
            : FormatPages(lines.Select(line => (line.PageNumber, line.Text)));
    }

    private async Task<string?> ReadOcrTextAsync(Guid fileId, List<string> unreadable)
    {
        var pages = await databaseReadService.GetOcrCachedPagesAsync(fileId, OcrServicesByPreference);

        return FormatPages(pages.SelectMany(page => ReadPage(page, unreadable)));
    }

    private static IEnumerable<(int PageNumber, string? Text)> ReadPage(
        OcrCachedPage page,
        List<string> unreadable)
    {
        var where = $"{page.OcrServiceName} page {page.PageNumber}";

        if (page.OcrServiceName != AzureOcrServiceName)
        {
            var lines = Deserialize<List<LineAndWords>>(page.Data, unreadable, where);

            return lines == null
                ? []
                : lines.Select(line => (page.PageNumber, (string?)line.Text));
        }

        // Azure's own options, not SerializerOptions: the payload carries a style-name enum that
        // only deserialises with JsonHelper's JsonStringEnumConverter.
        var readResult = Deserialize<ReadResult>(
            page.Data, unreadable, where, JsonHelper.GetSerializerOptions());

        return readResult?.Lines == null
            ? []
            : readResult.Lines.Select(line => (page.PageNumber, (string?)line.Text));
    }

    /// DocumentTextView in the portal parses this banner back out; the two must agree.
    private static string? FormatPages(IEnumerable<(int PageNumber, string? Text)> lines)
    {
        var pages = lines
            .Where(line => !string.IsNullOrWhiteSpace(line.Text))
            .GroupBy(line => line.PageNumber)
            .OrderBy(page => page.Key)
            .ToList();

        if (pages.Count == 0)
        {
            return null;
        }

        var text = new StringBuilder();

        foreach (var page in pages)
        {
            if (text.Length > 0)
            {
                text.Append("\n\n");
            }

            if (pages.Count > 1)
            {
                text.Append("--- Page ").Append(page.Key).Append(" ---\n");
            }

            text.Append(string.Join('\n', page.Select(line => line.Text!.TrimEnd())));
        }

        return text.Length == 0 ? null : text.ToString();
    }

    private static T? Deserialize<T>(
        string json,
        List<string> unreadable,
        string where,
        JsonSerializerOptions? options = null)
        where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, options ?? SerializerOptions);
        }
        catch (JsonException exception)
        {
            unreadable.Add($"{where} ({exception.Message})");
            ConsoleHelper.WriteLine(
                $"ERROR - {nameof(DocumentTextService)} - could not read {where} - {exception.Message}");

            return null;
        }
    }
}
