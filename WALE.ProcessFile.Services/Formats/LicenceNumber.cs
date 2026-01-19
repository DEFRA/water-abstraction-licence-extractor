using System.Text.RegularExpressions;
using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Services.Formats;

public partial class LicenceNumber(IDatabaseReadService? databaseReadService = null) : ILicenceNumberService
{
    private static ILicenceNumberService? _instance;

    public static ILicenceNumberService Instance
    {
        get => _instance ??
               throw new InvalidOperationException("LicenceNumber.Instance must be initialized before use.");
        set => _instance = value;
    }

    private Dictionary<string, List<LicenceIndexEntry>>? _licenceIndex;

    public async Task InitializeAsync()
    {
        if (databaseReadService != null)
        {
            var licences = await databaseReadService.GetNaldLicencesAsync();
            _licenceIndex = BuildIndex(licences);
        }
    }

    private class LicenceIndexEntry
    {
        public required NaldLicence NaldLicence { get; init; }
        public required List<string> Segments { get; init; }
    }

    private static Dictionary<string, List<LicenceIndexEntry>> BuildIndex(List<NaldLicence> licences)
    {
        var index = new Dictionary<string, List<LicenceIndexEntry>>();

        foreach (var licence in licences)
        {
            var normalizedKey = NormalizeLicenceNumber(licence.LicenceNumber);
            var segments = ExtractSegments(licence.LicenceNumber);

            var entry = new LicenceIndexEntry
            {
                NaldLicence = licence,
                Segments = segments
            };

            if (!index.TryGetValue(normalizedKey, out var entries))
            {
                entries = [];
                index[normalizedKey] = entries;
            }

            entries.Add(entry);
        }

        return index;
    }

    public static string NormalizeLicenceNumber(string licenceNumber)
        => new(licenceNumber.Where(c => char.IsLetterOrDigit(c) && c != '0').ToArray());

    public static List<string> ExtractSegments(string licenceNumber)
    {
        // Identify all separator characters (non-alphanumeric)
        var allSeparators = licenceNumber.Where(c => !char.IsLetterOrDigit(c)).Distinct().ToList();

        // If there are dots AND other separators, split on anything that is not alphanumeric or a dot,
        // otherwise just split on non-alphanumerics - to support correct segmentation of "1.2.3.4" vs. "1/2/3.1/4"
        var regex = allSeparators.Contains('.') && allSeparators.Count > 1
            ? NonAlphanumericOrDotRegex()
            : NonAlphanumericRegex();

        return regex
            .Split(licenceNumber)
            .Where(s => !string.IsNullOrEmpty(s))
            .Select(s => s.TrimStart('0'))
            .Select(s => string.IsNullOrEmpty(s) ? "0" : s)
            .ToList();
    }

    [GeneratedRegex(@"[^a-zA-Z0-9.]+")]
    private static partial Regex NonAlphanumericOrDotRegex();

    [GeneratedRegex(@"[^a-zA-Z0-9]+")]
    private static partial Regex NonAlphanumericRegex();

    public const string Constant = "LicenceNumber";

    // AA/123, AA/123/123, AA/123/123/123, 'AA 123 123 123' or AA.123.123.123 (and some other variations of this)
    public const string YorkshireRegexPatten =
        @"\b[0-9A-Z*&/.]{1,15}/([0-9]{2}|[0-9]/)[0-9A-Z*&/.]{1,15}\b|\b[0-9A-Z*&]{1,15}\.[0-9A-Z*&]{1,15}\.[0-9A-Z*&]{1,15}|(?<=\b)[0-9]{1,15}[ /][0-9ABRSG ]{2,15}[0-9]\b";

    public static List<string> FindLicenceNumbers(string? text)
    {
        return Instance.FindLicenceNumbers(text);
    }

    List<string> ILicenceNumberService.FindLicenceNumbers(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        var result = ((ILicenceNumberService)this).AnyIsLicenceNumber(
            [new DocumentLine { Columns = { new DocumentLineColumn(text) } }],
            new LabelToMatch(), false, out var outList);
        return result ? outList.Select(x => x.Text).ToList() : [];
    }

    public static bool AnyIsLicenceNumber(
        IEnumerable<DocumentLine?> lines,
        LabelToMatch label,
        bool isOcr,
        out List<DocumentLine> matchedLines)
    {
        return Instance.AnyIsLicenceNumber(lines, label, isOcr, out matchedLines);
    }

    bool ILicenceNumberService.AnyIsLicenceNumber(
        IEnumerable<DocumentLine?> lines,
        LabelToMatch label,
        bool isOcr,
        out List<DocumentLine> matchedLines)
    {
        matchedLines = [];

        // Flatten and validate columns
        var columnsToProcess = lines
            .Where(l => l != null)
            .SelectMany(l => l!.Columns.Select(c => (Line: l, Column: c)))
            .Where(x => IsValidColumnForProcessing(x.Column));

        // Flatten and validate sublines
        var subLinesToProcess = columnsToProcess
            .SelectMany(x => GetColumnSubLines(x.Column).Select(subLine => (x.Line, x.Column, subLine)))
            .Where(x => IsValidSubLine(x.subLine, x.Column));

        foreach (var (line, _, subLine) in subLinesToProcess)
        {
            var licenceNumberCandidates = LicenceNumbersRegex().Matches(subLine);

            if (licenceNumberCandidates.Count == 0)
            {
                continue;
            }

            foreach (Match match in licenceNumberCandidates)
            {
                var candidateText = match.Value;
                var normalizedCandidate = NormalizeLicenceNumber(candidateText);

                if (_licenceIndex == null || !_licenceIndex.TryGetValue(normalizedCandidate, out var entries))
                {
                    continue;
                }

                var candidateSegments = ExtractSegments(candidateText);

                foreach (var entry in entries)
                {
                    if (candidateSegments.SequenceEqual(entry.Segments))
                    {
                        // Passed all checks so add a clone of the line containing the matched NALD licence number
                        matchedLines.Add(line.Clone([new DocumentLineColumn(entry.NaldLicence.LicenceNumber)]));

                        // Exit early if we're looking for a single instance match
                        if (label.MultipleBehaviour is MultipleBehaviour.FindSingleInstanceOfLabelWithASingleValue)
                        {
                            return true;
                        }
                    }
                }
            }
        }

        return matchedLines.Count > 0;
    }

    private static bool IsValidColumnForProcessing(DocumentLineColumn column) =>
        !string.IsNullOrEmpty(column.Text)
        && column.Text.Any(char.IsDigit)
        && !DataHelper.IsCorruptedText(column.Text);

    private static bool IsValidSubLine(string subLine, DocumentLineColumn column) =>
        subLine.Length >= 4
        && (subLine.Contains(' ')
            || column.Text.Contains('/')
            || column.Text.Contains('.'));

    private static string[] GetColumnSubLines(DocumentLineColumn column)
    {
        const string splitChar = ",";

        var columnText = column.Text
            .Replace(". ", $"{splitChar} ")
            .Replace(" and", splitChar)
            .Replace(" for", splitChar)
            .Replace(" shall", splitChar)
            .Replace(" under", splitChar)
            .Replace(" from", splitChar)
            .Replace(" (", splitChar);

        columnText = SlashSpaceDigitRegex()
            .Replace(columnText, "/");

        var subLines = columnText.Split(splitChar);
        return subLines;
    }

    [GeneratedRegex(YorkshireRegexPatten)]
    private static partial Regex LicenceNumbersRegex();

    [GeneratedRegex(@"/ (?=\d)")]
    private static partial Regex SlashSpaceDigitRegex();
}