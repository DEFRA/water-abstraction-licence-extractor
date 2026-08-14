using System.Text.RegularExpressions;
using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WRADI.Core.AbstractionLicence.Interfaces;
using WRADI.Core.AbstractionLicence.Models;
using WRADI.DocumentType.AbstractionLicence.Models;

namespace WRADI.DocumentType.AbstractionLicence.Formats;

public partial class AbstractionLicenceNumber(
    List<NaldLicence> licences,
    Dictionary<string, NaldLicenceNumberHistory> licenceHistory) : ILicenceNumberService
{
    private readonly Dictionary<string, List<LicenceIndexEntry>> _licenceIndex =
        BuildIndex(licences ?? throw new ArgumentNullException());

    private readonly Dictionary<string, NaldLicenceNumberHistory> _licenceHistory = licenceHistory
        ?? throw new ArgumentNullException();

    private Dictionary<string, List<LicenceIndexEntry>> GetLicenceIndex() => _licenceIndex;

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

    [GeneratedRegex("[^a-zA-Z0-9.]+")]
    private static partial Regex NonAlphanumericOrDotRegex();

    [GeneratedRegex("[^a-zA-Z0-9]+")]
    private static partial Regex NonAlphanumericRegex();

    // AA/123, AA/123/123, AA/123/123/123, 'AA 123 123 123' or AA.123.123.123 (and some other variations of this)
    public const string YorkshireRegexPatten =
        @"\b[0-9A-Z*&/.]{1,15}/([0-9]{2}|[0-9]/)[0-9A-Z*&/.]{1,15}\b|\b[0-9A-Z*&]{1,15}\.[0-9A-Z*&]{1,15}\.[0-9A-Z*&]{1,15}|(?<=\b)[0-9]{1,15}[ /][0-9ABRSG ]{2,15}[0-9]\b";

    (bool Success, List<DocumentLine> MatchedLines)
            ILicenceNumberServiceCore.AnyIsLicenceNumber(
        IEnumerable<DocumentLine?> lines,
        LabelToMatch label,
        bool isOcr,
        Dictionary<string, object?> additionalInformationStore)
    {
        var matchedLines = new List<DocumentLine>();

        // Flatten and validate columns
        var columnsToProcess = lines
            .Where(l => l != null)
            .SelectMany(l => l!.Columns.Select(c => (Line: l, Column: c)))
            .Where(col => IsValidColumnForProcessing(col.Column, isOcr));

        // Flatten and validate sublines
        var subLinesToProcess = columnsToProcess
            .SelectMany(col => GetSubLines(col.Column.Text).Select(subLine => (col.Line, col.Column, subLine)))
            .Where(subLine => IsValidSubLine(subLine.subLine, subLine.Column.Text));

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
                    const string licenceNumberCandidatesNotInNaldKey = "LicenceNumberCandidatesNotInNald";
                    var notPresentInNaldList = new HashSet<string>();
                    
                    if (additionalInformationStore.TryGetValue(licenceNumberCandidatesNotInNaldKey, out var listObject))
                    {
                        notPresentInNaldList = (HashSet<string>)listObject!;
                    }

                    // Adding to a hashset never throws exception, even if it exists
                    if (notPresentInNaldList.Add(candidateText))
                    {
                        additionalInformationStore[licenceNumberCandidatesNotInNaldKey] = notPresentInNaldList;   
                    }
                    
                    continue;
                }

                var candidateSegments = ExtractSegments(candidateText);

                foreach (var entry in entries)
                {
                    if (!AllChecksMatch(candidateSegments, candidateText, entry))
                    {
                        continue;
                    }

                    var existingColumn = line.Columns.FirstOrDefault();
                    var candidateTextColumn = new DocumentLineColumn(DocumentLineColumn.TextToWords(
                        candidateText,
                        existingColumn?.OcrConfidence,
                        existingColumn?.Words.FirstOrDefault()?.Coordinates));
                    
                    // Passed all checks so add a clone of the line containing the matched NALD licence number
                    var matchedLine = line.Clone([candidateTextColumn]);
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

    public (bool HasSuccessor, List<NaldLicenceNumberHistory> History) AnyNewerLicenceNumber(
        string? licenceNumber)
    {
        var item = _licenceHistory.GetValueOrDefault(licenceNumber!.ToLower());
        var hasSuccessor = item != null;

        var history = new List<NaldLicenceNumberHistory>();

        while (item != null)
        {
            history.Add(item);
            item = _licenceHistory!.GetValueOrDefault(item.LicenceNumber);
        }
        
        return (hasSuccessor, history);
    }

    public List<NaldLicence> GetNaldLicences(string licenceNumber)
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
            .ToList();
    }

    public List<NaldLicence> ExtractNaldLicences(string? sourceText)
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
        && !DataHelper.IsCorruptedLine(column.Text, isOcr);

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
    public static partial Regex LicenceNumbersRegex();

    [GeneratedRegex(@"/ (?=\d)")]
    private static partial Regex SlashSpaceDigitRegex();
}