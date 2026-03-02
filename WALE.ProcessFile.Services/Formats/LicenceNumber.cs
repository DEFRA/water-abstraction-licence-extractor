using System.Text.RegularExpressions;
using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Services.Formats;

public partial class LicenceNumber : ILicenceNumberService
{
    private static ILicenceNumberService? _instance;

    public static ILicenceNumberService Instance
    {
        get => _instance ??
               throw new InvalidOperationException("LicenceNumber.Instance must be initialized before use.");
        set => _instance = value;
    }

    private readonly Dictionary<string, List<LicenceIndexEntry>> _licenceIndex;

    public LicenceNumber(List<NaldLicence> licences)
    {
        ArgumentNullException.ThrowIfNull(licences);
        _licenceIndex = BuildIndex(licences);
    }

    private Dictionary<string, List<LicenceIndexEntry>> GetLicenceIndex() => _licenceIndex;

    public class LicenceIndexEntry
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
            .Select(s => s.TrimStart('0').Replace(".", string.Empty))
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

    public static (bool Success, List<DocumentLine> MatchedLines) AnyIsLicenceNumber(
        IEnumerable<DocumentLine?> lines,
        LabelToMatch label,
        bool isOcr)
    {
        return Instance.AnyIsLicenceNumber(lines, label, isOcr);
    }

    (bool Success, List<DocumentLine> MatchedLines) ILicenceNumberService.AnyIsLicenceNumber(
        IEnumerable<DocumentLine?> lines,
        LabelToMatch label,
        bool isOcr)
    {
        var matchedLines = new List<DocumentLine>();

        // Flatten and validate columns
        var columnsToProcess = lines
            .Where(l => l != null)
            .SelectMany(l => l!.Columns.Select(c => (Line: l, Column: c)))
            .Where(x => IsValidColumnForProcessing(x.Column, isOcr));

        // Flatten and validate sublines
        var subLinesToProcess = columnsToProcess
            .SelectMany(x => GetSubLines(x.Column.Text).Select(subLine => (x.Line, x.Column, subLine)))
            .Where(x => IsValidSubLine(x.subLine, x.Column.Text));

        var licenceIndex = GetLicenceIndex();

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

                if (!licenceIndex.TryGetValue(normalizedCandidate, out var entries))
                {
                    continue;
                }

                var candidateSegments = ExtractSegments(candidateText);

                foreach (var entry in entries)
                {
                    if (!AllChecksMatch(candidateSegments, candidateText, entry))
                    {
                        continue;
                    }
                    
                    // Passed all checks so add a clone of the line containing the matched NALD licence number
                    var matchedLine = line.Clone([new DocumentLineColumn(candidateText)]);
                    matchedLine.AdditionalData ??= new Dictionary<string, object>();
                    matchedLine.AdditionalData.Add("NaldLicenceNumber", entry.NaldLicence.LicenceNumber);
                        
                    matchedLines.Add(matchedLine);
                    
                    // Exit early if we're looking for a single instance match
                    if (label.MultipleMatchBehaviour is MultipleMatchBehaviour.FindSingleInstanceOfLabelWithASingleValue)
                    {
                        return (true, matchedLines);
                    }
                }
            }
        }

        return (matchedLines.Count > 0, matchedLines);
    }

    public static List<NaldLicence> GetNaldLicences(string licenceNumber, short regionCode)
    {
        return Instance.GetNaldLicences(licenceNumber, regionCode);
    }

    List<NaldLicence> ILicenceNumberService.GetNaldLicences(string licenceNumber, short regionCode)
    {
        var normalized = NormalizeLicenceNumber(licenceNumber);

        var index = GetLicenceIndex();
        if (!index.TryGetValue(normalized, out var candidates))
        {
            return [];
        }

        var segments = ExtractSegments(licenceNumber);
        
        return candidates
            .Where(c => SegmentsMatch(segments, c.Segments))
            .Select(c => c.NaldLicence)
            .Where(l => l.RegionCode == regionCode)
            .ToList();
    }

    public static List<NaldLicence> ExtractNaldLicences(string? sourceText)
    {
        return Instance.ExtractNaldLicences(sourceText);
    }

    List<NaldLicence> ILicenceNumberService.ExtractNaldLicences(string? sourceText)
    {
        if (string.IsNullOrEmpty(sourceText) || !sourceText.Any(char.IsDigit))
        {
            return [];
        }

        var subLines = GetSubLines(sourceText)
            .Where(subLine => IsValidSubLine(subLine, sourceText));

        var resultList = new List<NaldLicence>();
        var licenceIndex = GetLicenceIndex();

        foreach (var subLine in subLines)
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

                if (!licenceIndex.TryGetValue(normalizedCandidate, out var entries))
                {
                    continue;
                }

                var candidateSegments = ExtractSegments(candidateText);

                resultList
                    .AddRange(entries.Where(entry => SegmentsMatch(candidateSegments, entry.Segments))
                        .Select(entry => entry.NaldLicence));
            }
        }

        return resultList
            .DistinctBy(l => new { l.LicenceNumber, l.RegionCode })
            .ToList();
    }

    private static bool AnySourceNumberSectionHasMoreZeroes(string sourceLinkedLicenceNumber, string naldLinkedLicenceNumber)
    {
        sourceLinkedLicenceNumber = sourceLinkedLicenceNumber
            .Replace(" ", "/")
            .Replace(".", "/");
        
        naldLinkedLicenceNumber = naldLinkedLicenceNumber
            .Replace(" ", "/")
            .Replace(".", "/");

        var sourceSegments = sourceLinkedLicenceNumber.Split('/');
        var naldSegments = naldLinkedLicenceNumber.Split('/');

        if (naldSegments.Length == 1)
        {
            return false;
        }
        
        var index = 0;
        
        foreach (var sourceSegment in sourceSegments)
        {
            if (string.IsNullOrWhiteSpace(sourceSegment))
            {
                continue;
            }
            
            var naldSegment = naldSegments.Length > index ? naldSegments[index++] : null;

            var sourceSegmentZeroCount = sourceSegment.Count(c => c == '0');
            var naldSegmentZeroCount = naldSegment?.Count(c => c == '0') ?? 0;

            if (sourceSegmentZeroCount > naldSegmentZeroCount)
            {
                return true;
            }
        }
        
        return false;
    }
    
    private static bool NumberOfSectionsMatch(string sourceLinkedLicenceNumber, string naldLinkedLicenceNumber)
    {
        if (!sourceLinkedLicenceNumber.Contains('/'))
        {
            return true;
        }
        
        var sourceParts =  sourceLinkedLicenceNumber.Split('/');
        var naldParts = naldLinkedLicenceNumber.Split('/');

        if (naldParts.Length == 1)
        {
            return true;
        }
        
        var countsMatch = sourceParts.Count(c => !string.IsNullOrEmpty(c))
            == naldParts.Count(c => !string.IsNullOrEmpty(c));

        if (!countsMatch)
        {
            return false;
        }

        return true;
    }

    private static bool AllChecksMatch(List<string> candidateSegments, string candidateText, LicenceIndexEntry entry)
    {
        if (!SegmentsMatch(candidateSegments, entry.Segments))
        {
            return false;
        }

        if (!NumberOfSectionsMatch(candidateText, entry.NaldLicence.LicenceNumber))
        {
            return false;
        }

        if (AnySourceNumberSectionHasMoreZeroes(candidateText, entry.NaldLicence.LicenceNumber))
        {
            return false;
        }

        return true;
    }
    
    public static bool SegmentsMatch(
        List<string> segments1,
        List<string> segments2)
    {
        var segments1String = string.Join("/", segments1);
        var segments2String = string.Join("/", segments2);

        if (segments1String == segments2String)
        {
            return true;
        }

        var segments1Index = 0;
        var segments2Index = 0;

        while (segments1Index < segments1String.Length
            && segments2Index < segments2String.Length)
        {
            var segment1Char = segments1String[segments1Index];
            var segment2Char = segments2String[segments2Index];

            // If both characters match, advance both iterators
            if (segment1Char == segment2Char)
            {
                segments1Index++;
                segments2Index++;
                
                continue;
            }

            // Handle segment break in s1: s2 can have zeroes or continue with next character
            if (segment1Char == '/')
            {
                if (segment2Char == '0')
                {
                    segments2Index++;
                }
                else
                {
                    segments1Index++;
                }

                continue;
            }

            // Handle segment break in s2: s1 can have zeroes or continue with next character
            if (segment2Char == '/')
            {
                if (segment1Char == '0')
                {
                    segments1Index++;
                }
                else
                {
                    segments2Index++;
                }

                continue;
            }

            // Characters don't match and no special rules apply
            return false;
        }

        // Both strings should be fully consumed
        return segments1Index == segments1String.Length && segments2Index == segments2String.Length;
    }

    private static bool IsValidColumnForProcessing(DocumentLineColumn column, bool isOcr) =>
        !string.IsNullOrEmpty(column.Text)
        && column.Text.Any(char.IsDigit)
        && !DataHelper.IsCorruptedText(column.Text, isOcr);

    private static bool IsValidSubLine(string subLine, string fullText) =>
        subLine.Length >= 4
        && (subLine.Contains(' ')
            || fullText.Contains('/')
            || fullText.Contains('.'));

    private static string[] GetSubLines(string text)
    {
        const string splitChar = ",";

        text = text
            .Replace(". ", $"{splitChar} ")
            .Replace(" and", splitChar)
            .Replace(" for", splitChar)
            .Replace(" shall", splitChar)
            .Replace(" under", splitChar)
            .Replace(" from", splitChar)
            .Replace(" (", splitChar);

        text = SlashSpaceDigitRegex()
            .Replace(text, "/");

        var subLines = text.Split(splitChar);
        return subLines;
    }

    [GeneratedRegex(YorkshireRegexPatten)]
    private static partial Regex LicenceNumbersRegex();

    [GeneratedRegex(@"/ (?=\d)")]
    private static partial Regex SlashSpaceDigitRegex();
}