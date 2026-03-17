using System.Globalization;
using CsvHelper;
using WALE.ProcessFile.Core.Helpers;

namespace WALE.Tools.Helpers;

/// <summary>
/// Helper class containing common functionality used across tool classes
/// </summary>
public static class ToolHelper
{
    /// <summary>
    /// Writes a collection of records to a CSV file
    /// </summary>
    /// <typeparam name="T">Type of records to write</typeparam>
    /// <param name="data">Data to write to CSV</param>
    /// <param name="fileName">Name of the CSV file (without extension)</param>
    /// <param name="outputFolder">Output folder path</param>
    /// <returns>The full path of the created CSV file</returns>
    private static async Task<string> WriteCsvAsync<T>(IEnumerable<T> data, string fileName, string outputFolder)
    {
        var csvFileName = $"{fileName}-{DateTime.Today:yyyyMMdd}.csv";
        var fullPath = Path.Combine(outputFolder, csvFileName);

        await using var writer = new StreamWriter(fullPath);
        await using var csv = new CsvWriter(writer, new CultureInfo("en-GB"));

        await csv.WriteRecordsAsync(data);

        return fullPath;
    }

    /// <summary>
    /// Prints a summary of processed items grouped by a specified property
    /// </summary>
    /// <typeparam name="T">Type of items to summarize</typeparam>
    /// <typeparam name="TKey">Type of the grouping key</typeparam>
    /// <param name="data">Data to summarize</param>
    /// <param name="keySelector">Function to select the grouping key</param>
    /// <param name="summaryTitle">Title for the summary section</param>
    private static void PrintSummary<T, TKey>(IEnumerable<T> data, Func<T, TKey> keySelector, string summaryTitle = "Summary")
    {
        var summary = data
            .GroupBy(keySelector)
            .Select(g => new { g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count);

        ConsoleHelper.WriteLine($"\n{summaryTitle}:");
        
        foreach (var item in summary)
        {
            ConsoleHelper.WriteLine($"  {item.Key}: {item.Count} items");
        }
    }

    /// <summary>
    /// Generates a CSV report with summary output
    /// </summary>
    /// <typeparam name="T">Type of records to write</typeparam>
    /// <typeparam name="TKey">Type of the summary grouping key</typeparam>
    /// <param name="data">Data to write to CSV</param>
    /// <param name="fileName">Name of the CSV file (without extension)</param>
    /// <param name="outputFolder">Output folder path</param>
    /// <param name="keySelector">Function to select the grouping key for summary</param>
    /// <param name="processedItemsDescription">Description of what was processed (e.g., "files", "records")</param>
    /// <param name="summaryTitle">Title for the summary section</param>
    /// <returns>The full path of the created CSV file</returns>
    public static async Task GenerateCsvReportWithSummaryAsync<T, TKey>(
        IEnumerable<T> data,
        string fileName,
        string outputFolder,
        Func<T, TKey> keySelector,
        string processedItemsDescription = "items",
        string summaryTitle = "Summary")
    {
        var dataList = data.ToList();

        // Write CSV file
        var fullPath = await WriteCsvAsync(dataList, fileName, outputFolder);

        // Print completion message and summary
        ConsoleHelper.WriteLine($"Report completed. Results written to: {fullPath}");
        ConsoleHelper.WriteLine($"Total {processedItemsDescription} processed: {dataList.Count}");

        // Print summary
        PrintSummary(dataList, keySelector, summaryTitle);
    }

    /// <summary>
    /// Creates a standard output folder file path with date stamp
    /// </summary>
    /// <param name="fileName">Base file name</param>
    /// <param name="outputFolder">Output folder path</param>
    /// <returns>Full file path with date stamp</returns>
    public static string CreateDateStampedFileName(string fileName, string outputFolder)
    {
        var dateStampedFileName = $"{fileName}-{DateTime.Today:yyyyMMdd}.csv";
        return Path.Combine(outputFolder, dateStampedFileName);
    }
}
