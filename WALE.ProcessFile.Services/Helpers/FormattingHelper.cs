using WALE.ProcessFile.Services.Constants;
using WALE.ProcessFile.Services.Models;

namespace WALE.ProcessFile.Services.Helpers;

public static class FormattingHelper
{
    public static List<DocumentLine> RemoveMultipleBlankLines(IEnumerable<DocumentLine> sourceList)
    {
        var trimmedList = TrimList(sourceList);
        
        var returnList = new List<DocumentLine>();
        var previousLineWasBlank = false;
        
        foreach (var line in trimmedList.Where(line => !previousLineWasBlank || !string.IsNullOrEmpty(line.Text)))
        {
            previousLineWasBlank = string.IsNullOrEmpty(line.Text);
            returnList.Add(line);
        }

        return returnList;
    }
    
    public static string? TrimFormatting(string? text, bool trimPunctuation)
    {
        var trimmed = text?.Trim();
        if (!trimPunctuation) return trimmed;
        
        while (trimmed?.Length >= 1
           && trimmed[0] != '('
           && (char.IsPunctuation(trimmed[0])
               || char.IsSymbol(trimmed[0])
               || char.IsWhiteSpace(trimmed[0])))
        {
            trimmed = trimmed[1..];
        }

        while (trimmed?.Length >= 1
           && trimmed[^1] != ')'
           && trimmed[^1] != ':'
           && trimmed[^1] != '/'
           && (char.IsPunctuation(trimmed[^1])
               || char.IsSymbol(trimmed[^1])
               || char.IsWhiteSpace(trimmed[^1])))
        {
            trimmed = trimmed[..^1];
        }

        return trimmed;
    }
    
    public static void Standardise(List<DocumentLineColumn> columns)
    {
        const string singleQuoteChar = "'";        
        const string doubleQuoteChar = "\"";
        const string asteriskString = "*";

        foreach (var column in columns)
        {
            column.Text = column.Text
                .Trim()
                .Replace("‘‘", doubleQuoteChar)
                .Replace("’’", doubleQuoteChar)
                .Replace("‘", singleQuoteChar)
                .Replace("’", singleQuoteChar)
                .Replace("“", doubleQuoteChar)
                .Replace("”", doubleQuoteChar)
                .Replace("'\"", doubleQuoteChar)
                .Replace("\u00b0", asteriskString) // degree character, OCR thinks it sees it for some small text
                .Replace("  ", PositionConstants.SpaceString)
                .Replace("\"\"", doubleQuoteChar);
        }
    }

    public static bool IsPageEmpty(string? input) => IsNullOrEmptyWhitespaceOrPunctuation(input);
    
    public static bool IsLineEmpty(DocumentLine? input) => IsNullOrEmptyWhitespaceOrPunctuation(input?.Text);

    public static bool IsNullOrEmptyWhitespaceOrPunctuation(string? input)
    {
        if (input == null)
        {
            return true;
        }

        var noPunctuationInput = new string(input.Where(c => !char.IsPunctuation(c)).ToArray());
        return string.IsNullOrWhiteSpace(noPunctuationInput);
    }
    
    public static void NullOutSubLabels(IReadOnlyList<LabelGroupResult> matches)
    {
        foreach (var match in matches)
        {
            if (match.MatchedLabel != null)
            {
                match.MatchedLabel.SubLabels = null;
            }

            NullOutSubLabels(match.SubResults);
        }
    }
    
    public static void RemoveRemoves(
        LabelGroupResult labelGroupResult,
        IReadOnlyList<string>? removedLines)
    {
        if (labelGroupResult.MatchedLabel?.Remove == null)
        {
            return;
        }
        
        labelGroupResult.MatchedLabel.Remove =
            labelGroupResult.MatchedLabel.Remove!.Where(removeLine => removedLines?.Contains(removeLine.Text) == true).ToList();

        if (labelGroupResult.MatchedLabel.Remove.Count == 0)
        {
            labelGroupResult.MatchedLabel.Remove = null;
        }
    }
    
    private static IEnumerable<DocumentLine> TrimList(IEnumerable<DocumentLine> sourceList)
    {
        return sourceList
            .SkipWhile(x => string.IsNullOrWhiteSpace(x.Text))
            .Reverse()
            .SkipWhile(x => string.IsNullOrWhiteSpace(x.Text))
            .Reverse()
            .ToList();
    }
}