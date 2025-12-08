using System.Text;
using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Core.Helpers;

public static class FormattingHelper
{
    public static string? ToZeroFormattingRemoveLeadingZeroes(string? formattedLicenceNumber)
    {
        if (string.IsNullOrEmpty(formattedLicenceNumber))
        {
            return formattedLicenceNumber;
        }

        var licenceNumber = formattedLicenceNumber.Replace("-", "/");

        var parts = licenceNumber.Split('/');
        var sb = new StringBuilder();

        foreach (var part in parts)
        {
            if (part.StartsWith('0'))
            {
                var partWithoutLeadingZero = part[1..];
                sb.Append(partWithoutLeadingZero);
                
                continue;
            }
            
            sb.Append(part);
        }

        return sb.ToString();
    }
    
    public static string? NoneSeperatedToNaldLicenceNumber(string? noneSeperatedLicenceNumber)
    {
        if (string.IsNullOrEmpty(noneSeperatedLicenceNumber))
        {
            return noneSeperatedLicenceNumber;
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
        
        var section1 = noneSeperatedLicenceNumber[0].ToString();

        if (section1 == "J" || section1 == "4" || section1 == "7")
        {
            section1 = "1";
        }

        var section2StartPoint = 1;
        var section2Length = 2;
        
        var section3StartPoint = 3;
        var section3Length = 2;
        
        var section4StartPoint = 5;
        
        if (noneSeperatedLicenceNumber.StartsWith("NE"))
        {
            section1 = "NE";
            
            section2StartPoint += 1;
            section2Length = 3;
            
            section3StartPoint += 2;
            section3Length = 4;
            
            section4StartPoint += 4;
        }
        else if (noneSeperatedLicenceNumber.StartsWith("0"))
        {
            section1 = noneSeperatedLicenceNumber[1].ToString();
            
            section2StartPoint += 1;
            section3StartPoint += 1;
            section4StartPoint += 1;
        }

        if (noneSeperatedLicenceNumber.Length < 3)
        {
            return noneSeperatedLicenceNumber;
        }
        
        var section2 = noneSeperatedLicenceNumber.Substring(section2StartPoint, section2Length);

        if (noneSeperatedLicenceNumber.Length < 5)
        {
            return $"{section1}/{section2}";
        }
        
        var section3 = noneSeperatedLicenceNumber.Substring(section3StartPoint, section3Length);
        var section4 = noneSeperatedLicenceNumber[section4StartPoint..];
        
        // Pad part 4 with zeroes (needs to have 3 digits)
        section4 = section4.Where(char.IsDigit).Count() switch
        {
            1 => $"00{section4}",
            2 => $"0{section4}",
            _ => section4
        };

        if (section4.Length > 3)
        {
            if (section4.StartsWith("S"))
            {
                var rest = section4[1..];
                if (rest is ['0', _, _, _])
                {
                    rest = rest[1..];
                }
                
                section4 = $"S/{rest}";
            }
            else
            {
                section4 = section4[..3] + "/" + section4[3..];   
            }
        }

        if (section4.EndsWith("/A") || section4.EndsWith("/B") || section4.EndsWith("/C"))
        {
            section4 = section4
                .Replace("/A", "A")
                .Replace("/B", "B")
                .Replace("/C", "C");                
        }
        
        if (section4.Contains("R01") && !section4.Contains("/R01"))
        {
            var section4Parts = section4.Split('/');
            var prePart = section4Parts[0];
            var ro1Part = section4Parts[1];
            
            var r01Position = ro1Part.IndexOf("R01", StringComparison.Ordinal);
            var preText = ro1Part[..r01Position];

            prePart += preText;
            ro1Part = ro1Part[r01Position..];

            section4 = $"{prePart}/{ro1Part}";
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
                ? NoneSeperatedToNaldLicenceNumber(part1.Replace("/", string.Empty))
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
                ? NoneSeperatedToNaldLicenceNumber($"{part1}/{part2}".Replace("/", string.Empty))
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
                ? NoneSeperatedToNaldLicenceNumber($"{part1}/{part2}/{part3}".Replace("/", string.Empty))
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
                ? NoneSeperatedToNaldLicenceNumber($"{part1}/{part2}/{part3}/{part4}".Replace("/", string.Empty))
                : $"{part1}/{part2}/{part3}/{part4}";
        }

        var part5 = parts[4];
        
        return startsWithDigit && usesSlashes
            ? NoneSeperatedToNaldLicenceNumber($"{part1}/{part2}/{part3}/{part4}/{part5}".Replace("/", string.Empty))
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