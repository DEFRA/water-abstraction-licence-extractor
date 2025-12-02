using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Core.Helpers;

public static class FormattingHelper
{
    public static string? ToNaldLicenceNumber(string? noneSeperatedLicenceNumber)
    {
        if (string.IsNullOrEmpty(noneSeperatedLicenceNumber))
        {
            return noneSeperatedLicenceNumber;
        }

        if (noneSeperatedLicenceNumber.StartsWith("NE"))
        {
            // TODO something
        }
        
        return Yorkshire1_ToNaldLicenceNumber(noneSeperatedLicenceNumber);
    }

    public static string? PadLicenceNumber(string? licenceNumber)
    {
        if (string.IsNullOrEmpty(licenceNumber))
        {
            return licenceNumber;
        }

        if (licenceNumber.StartsWith("NE"))
        {
            // TODO something
        }
        
        if (licenceNumber.Contains("*"))
        {
            return licenceNumber;
        }
        
        if (licenceNumber.Contains("I") || licenceNumber.Contains("S"))
        {
            return licenceNumber;
        }
        
        if (licenceNumber.StartsWith('J'))
        {
            licenceNumber = '1' + licenceNumber[1..];
        }
        if (licenceNumber.StartsWith('4'))
        {
            licenceNumber = '1' + licenceNumber[1..];
        }
        if (licenceNumber.StartsWith('7'))
        {
            licenceNumber = '1' + licenceNumber[1..];
        }
        
        var numberOfSlashes = licenceNumber.Count(c => c == '/');
        
        if (numberOfSlashes is 1 or 2)
        {
            return licenceNumber;
        }

        if (numberOfSlashes == 3 && licenceNumber.Split('/')[0].Length == 2)
        {
            return licenceNumber;
        }
        
        return Yorkshire1_PadLicenceNumber(licenceNumber);
    }

    private static string? Yorkshire1_ToNaldLicenceNumber(string? noneSeperatedLicenceNumber)
    {
        if (string.IsNullOrEmpty(noneSeperatedLicenceNumber))
        {
            return noneSeperatedLicenceNumber;
        }
        
        var section1 = noneSeperatedLicenceNumber[0];

        if (section1 == 'J' || section1 == '4' || section1 == '7')
        {
            section1 = '1';
        }

        if (noneSeperatedLicenceNumber.Length < 3)
        {
            return noneSeperatedLicenceNumber;
        }
        
        var section2 = noneSeperatedLicenceNumber.Substring(1, 2);

        if (noneSeperatedLicenceNumber.Length < 5)
        {
            return $"{section1}/{section2}";
        }
        
        var section3 = noneSeperatedLicenceNumber.Substring(3, 2);
        var section4 = noneSeperatedLicenceNumber[5..];
        
        // Pad part 4 with zeroes (needs to have 3 digits)
        section4 = section4.Where(char.IsDigit).Count() switch
        {
            1 => $"00{section4}",
            2 => $"0{section4}",
            _ => section4
        };

        if (section4.Length > 3)
        {
            section4 = section4[..3] + "/" + section4[3..];;
        }
        
        return $"{section1}/{section2}/{section3}/{section4}";
    }

    private static string? Yorkshire1_PadLicenceNumber(string? licenceNumber)
    {
        if (string.IsNullOrEmpty(licenceNumber))
        {
            return licenceNumber;
        }

        var startsWithDigit = char.IsDigit(licenceNumber[0]);
        var usesSlashes = true;
        
        // Replace dots with slashes IF its all dots
        if (licenceNumber.Contains('.') && !licenceNumber.Contains('/'))
        {
            licenceNumber = licenceNumber.Replace(".", "/");
            usesSlashes = false;
        }
        
        // Replace spaches with slashes IF its all spaces
        if (licenceNumber.Contains(' ') && !licenceNumber.Contains('/'))
        {
            licenceNumber = licenceNumber.Replace(" ", "/");
            usesSlashes = false;            
        }
        
        var parts = licenceNumber.Split('/');
        
        var part1 = parts[0];

        if (parts.Length < 2)
        {
            return startsWithDigit && usesSlashes
                ? ToNaldLicenceNumber(part1.Replace("/", string.Empty))
                : part1;
        }
        
        var part2 = parts[1];
        
        if (part2.Length == 1)
        {
            part2 = $"0{part2}";
        }
        
        if (parts.Length < 3)
        {
            return startsWithDigit && usesSlashes
                ? ToNaldLicenceNumber($"{part1}/{part2}".Replace("/", string.Empty))
                : $"{part1}/{part2}";
        }
        
        var part3 = parts[2];

        if (part3.Length == 1)
        {
            part3 = $"0{part3}";
        }

        if (parts.Length < 4)
        {
            return startsWithDigit && usesSlashes
                ? ToNaldLicenceNumber($"{part1}/{part2}/{part3}".Replace("/", string.Empty))
                : $"{part1}/{part2}/{part3}";
        }
        
        var part4 = parts[3];

        // Pad part 4 with zeroes (needs to have 3 digits)
        part4 = part4.Where(char.IsDigit).Count() switch
        {
            1 => $"00{part4}",
            2 => $"0{part4}",
            _ => part4
        };
        
        if (parts.Length < 5)
        {
            return startsWithDigit && usesSlashes
                ? ToNaldLicenceNumber($"{part1}/{part2}/{part3}/{part4}".Replace("/", string.Empty))
                : $"{part1}/{part2}/{part3}/{part4}";
        }

        var part5 = parts[4];
        
        return startsWithDigit && usesSlashes
            ? ToNaldLicenceNumber($"{part1}/{part2}/{part3}/{part4}/{part5}".Replace("/", string.Empty))
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