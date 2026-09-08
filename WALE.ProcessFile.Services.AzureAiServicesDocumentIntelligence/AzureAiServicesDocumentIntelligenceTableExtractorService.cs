using System.Text.Json;
using Azure;
using Azure.AI.DocumentIntelligence;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Services.AzureAiServicesDocumentIntelligence.Models;

namespace WALE.ProcessFile.Services.AzureAiServicesDocumentIntelligence;

public class AzureAiServicesDocumentIntelligenceTableExtractorService(
    string endpoint,
    string key,
    ICacheService cacheService) : ITableExtractorService
{
    // Distinct from AzureAiServicesDocumentIntelligenceOcrDataExtractorService.Name
    // ("AzureAiServicesDocumentIntelligenceOcr", which calls "prebuilt-read") - this is a
    // deliberately separate cache namespace so "prebuilt-layout" results never collide with
    // "prebuilt-read" results for the same file. OcrServiceName is used as a literal
    // path/query/key discriminator by every ICacheService backend, so a distinct name alone
    // is sufficient for cache safety.
    public string Name => "AzureAiServicesDocumentIntelligenceLayoutOcr";

    private readonly DocumentIntelligenceClient _client = CreateClient(endpoint, key);

    public async Task<IReadOnlyList<OcrTable>> GetTablesAsync(
        byte[] documentBytes,
        Guid fileId,
        int processRunId)
    {
        // One call analyses the whole document (prebuilt-layout supports multi-page PDFs
        // directly via this async submit+poll pattern), so PageNumber/ImageNumber are fixed
        // sentinels here, not per-page/per-image multipliers - there is exactly one cache
        // entry per file for this service.
        var request = new OcrServiceImageTextCacheRequest
        {
            PageNumber = 1,
            ImageNumber = 0,
            FileId = fileId,
            OcrServiceName = Name,
            ProcessRunId = processRunId
        };

        var cacheText = await cacheService.GetOcrImageTextAsync(request);

        if (!string.IsNullOrEmpty(cacheText))
        {
            var cachedTables = JsonSerializer.Deserialize<List<DeserialisableDocumentIntelligenceTable>>(
                cacheText,
                JsonHelper.GetSerializerOptions());

            return ToOcrTables(cachedTables!);
        }

        var analyzeDocumentOptions = new AnalyzeDocumentOptions(
            "prebuilt-layout",
            BinaryData.FromBytes(documentBytes));

        Operation<AnalyzeResult> documentResult = await _client.AnalyzeDocumentAsync(
            WaitUntil.Completed,
            analyzeDocumentOptions);

        var tables = documentResult.Value.Tables
            .Select(table =>
            {
                var pageNumber = table.BoundingRegions.Count > 0 ? table.BoundingRegions[0].PageNumber : 1;
                return DeserialisableDocumentIntelligenceTable.FromDocumentTable(table, pageNumber);
            })
            .ToList();

        var data = JsonSerializer.Serialize(tables, JsonHelper.GetSerializerOptions());
        await cacheService.SaveOcrImageTextAsync(request, data);

        return ToOcrTables(tables);
    }

    private static IReadOnlyList<OcrTable> ToOcrTables(List<DeserialisableDocumentIntelligenceTable> tables)
    {
        return tables.Select(t => new OcrTable
        {
            PageNumber = t.PageNumber,
            RowCount = t.RowCount,
            ColumnCount = t.ColumnCount,
            Cells = (t.Cells ?? []).Select(c => new OcrTableCell
            {
                RowIndex = c.RowIndex,
                ColumnIndex = c.ColumnIndex,
                Content = c.Content,
                Left = c.Polygon is { Count: > 0 } ? c.Polygon[0] : null,
                Top = c.Polygon is { Count: > 1 } ? c.Polygon[1] : null
            }).ToList()
        }).ToList();
    }

    private static DocumentIntelligenceClient CreateClient(string endpoint, string key)
    {
        var credential = new AzureKeyCredential(key);
        return new DocumentIntelligenceClient(new Uri(endpoint), credential);
    }
}
