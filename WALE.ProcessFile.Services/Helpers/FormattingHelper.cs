using WALE.ProcessFile.Models;
using WALE.ProcessFile.Models.Constants;

namespace WALE.ProcessFile.Services.Helpers;

public static class FormattingHelper
{
    public static string? ToNaldLicenceNumber(string? licenceNumber)
    {
        if (string.IsNullOrEmpty(licenceNumber))
        {
            return licenceNumber;
        }

        return $"{licenceNumber[0]}/{licenceNumber.Substring(1, 2)}/{licenceNumber.Substring(3, 2)}/{licenceNumber[5..]}";
    }

    public static string? TransformLicenceNumber(string? licenceNumber)
    {
        if (string.IsNullOrEmpty(licenceNumber))
        {
            return licenceNumber;
        }

        // Replace dots with slashes IF its all dots
        if (licenceNumber.Contains('.') && !licenceNumber.Contains('/'))
        {
            licenceNumber = licenceNumber.Replace(".", "/");
        }
        
        var parts = licenceNumber.Split('/');
        
        if (parts.Length < 4)
        {
            return licenceNumber;
        }
        
        var part1 = parts[0];
        var part2 = parts[1];
        var part3 = parts[2];
        var part4 = parts[3];
        var part5 = parts.Length >= 5 ? parts[4] : null;
        
        if (part3.Length == 1)
        {
            part3 = $"0{part3}";
        }

        // Pad part 4 with zeroes (needs to have 3 digits)
        part4 = part4.Where(char.IsDigit).Count() switch
        {
            1 => $"00{part4}",
            2 => $"0{part4}",
            _ => part4
        };

        return parts.Length == 4 ?
            $"{part1}/{part2}/{part3}/{part4}"
            : $"{part1}/{part2}/{part3}/{part4}/{part5}";
    }
    
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
    
    public static string? TrimFormatting(
        string? text,
        bool trimPunctuationStart,
        bool trimPunctuationEnd)
    {
        var trimmed = text?.Trim();

        if (trimPunctuationStart)
        {
            while (trimmed?.Length >= 1
               && trimmed[0] != '('
               && (char.IsPunctuation(trimmed[0])
                   || char.IsSymbol(trimmed[0])
                   || char.IsWhiteSpace(trimmed[0])))
            {
                trimmed = trimmed[1..];
            }
        }

        if (trimPunctuationEnd)
        {
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
            column.Text = column.Text.Trim();

            if (column.Text.Contains("‘‘"))
            {
                column.Text = column.Text.Replace("‘‘", doubleQuoteChar);
            }
            
            if (column.Text.Contains("’’"))
            {
                column.Text = column.Text.Replace("’’", doubleQuoteChar);
            }
            
            if (column.Text.Contains('‘'))
            {
                column.Text = column.Text.Replace("‘", singleQuoteChar);
            }
                
            if (column.Text.Contains('’'))
            {
                column.Text = column.Text.Replace("’", singleQuoteChar);
            }
            
            if (column.Text.Contains('“'))
            {
                column.Text = column.Text.Replace("“", doubleQuoteChar);
            }
            
            if (column.Text.Contains('”'))
            {
                column.Text = column.Text.Replace("”", doubleQuoteChar);
            }
            
            if (column.Text.Contains("'\""))
            {
                column.Text = column.Text.Replace("'\"", doubleQuoteChar);
            }
            
            if (column.Text.Contains('°'))
            {
                column.Text =
                    column.Text.Replace("\u00b0",
                        asteriskString); // degree character, OCR thinks it sees it for some small text
            }
            
            if (column.Text.Contains("  "))
            {
                column.Text = column.Text.Replace("  ", PositionConstants.SpaceString);
            }

            if (column.Text.Contains("\"\""))
            {
                column.Text = column.Text.Replace("\"\"", doubleQuoteChar);
            }
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
    
    private static IReadOnlyList<DocumentLine> TrimList(IEnumerable<DocumentLine> sourceList)
    {
        return sourceList
            .SkipWhile(x => string.IsNullOrWhiteSpace(x.Text))
            .Reverse()
            .SkipWhile(x => string.IsNullOrWhiteSpace(x.Text))
            .Reverse()
            .ToList();
    }
}