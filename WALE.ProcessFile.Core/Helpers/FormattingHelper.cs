using System.Text;
using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Core.Helpers;

public static class FormattingHelper
{
    public static string? StripForComparison(string? formattedLicenceNumber)
    {
        if (string.IsNullOrEmpty(formattedLicenceNumber))
        {
            return null;
        }

        if (IsNeLicenceNumber(formattedLicenceNumber))
        {
            return StripForComparison_NE(formattedLicenceNumber);
        }

        var licenceNumber = formattedLicenceNumber
            .Replace("//", "/")
            .Replace(".", "/")
            .Replace(" ", "/")
            .Replace("-", "/");

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

        var str = sb.ToString();

        if (str.EndsWith('0'))
        {
            var partWithoutTrailingZero = str[..^1];
            return partWithoutTrailingZero.Replace("0", string.Empty) + "0";
        }
        
        return str.Replace("0", string.Empty);
    }

    private static string? StripForComparison_NE(string? formattedLicenceNumber)
    {
        var licenceNumber = ToFullLicenceNumber_NE(formattedLicenceNumber);
        return licenceNumber?.Replace("/", string.Empty);
    }

    private static string? ToFullLicenceNumber_NE(string? licenceNumber)
    {
        if (string.IsNullOrEmpty(licenceNumber))
        {
            return licenceNumber;
        }

        licenceNumber = licenceNumber
            .Replace(".", string.Empty)
            .Replace(" ", string.Empty)
            .Replace("-", string.Empty)
            .Replace("/", string.Empty);

        var parts = new List<string>();
        var remainingLicenceNumber = licenceNumber;
        
        // [1/2]/12/01/012
        if (remainingLicenceNumber[0] == '1' || remainingLicenceNumber[0] == '2')
        {
            // Examples
            // 2/27/29/31 (goes into NALD as 22729031 - 0 is padding to part 4)
            // 2/27/29/059 (22729059)
            // 2/27/28/285 (22728285)
            // 1/22/02/087 (12202087)
            // 1/22/2/43 (12202043 - 0 is padded in part 3 and part 4)
            // 1/24/4/016 (12404016 - 0 is padded in part 3)
            
            // Part 1 - 1
            var part1 = remainingLicenceNumber[..1];
            remainingLicenceNumber = remainingLicenceNumber[1..];
            
            // Part 2 - 12
            var part2 = remainingLicenceNumber[..2];
            remainingLicenceNumber = remainingLicenceNumber[2..];

            parts.Add(part1);
            parts.Add(part2);
            
            var splitOnR = remainingLicenceNumber.Split('R');

            if (splitOnR.Length >= 2)
            {
                splitOnR =
                [
                    splitOnR[0],
                    'R' + splitOnR[1]
                ];
            }
            
            var preRSectionAll = splitOnR[0];
            var preRSection = string.Join(string.Empty, preRSectionAll.Where(char.IsDigit).ToArray());

            if (preRSectionAll != preRSection)
            {
                if (splitOnR.Length == 1)
                {
                    splitOnR =
                    [
                        preRSection,
                        preRSectionAll[preRSection.Length..]
                    ];
                }
                else
                {
                    splitOnR =
                    [
                        preRSection,
                        preRSectionAll[preRSection.Length..] + splitOnR[1]
                    ];
                }
            }
            
            // Part 3 - Is 1 or 2 long, NALD wants it as 2
            // Part 4 - 12 (if part 3 has length 1) or 123 (if part 3 has length 2, new) - NALD has as 123
            
            if (preRSection.Length == 5)
            {
                // Part 3 - 12
                var part3 = preRSection[..2];
                preRSection = preRSection[2..];
                
                // Part 4 - 123
                var part4 = preRSection[..3];
                
                parts.Add(part3);
                parts.Add(part4);
            }
            else if (preRSection.Length == 4)
            {
                var firstChar = preRSection[0];
                var secondChar = preRSection[1];
                
                string? part3;
                string? part4;                
                
                // Definately needs padding as only valid range is 1-34 for this region
                if (firstChar is '4' or '5' or '6' or '7' or '8' or '9')
                {
                    // Part 3 - 1
                    part3 = "0" + preRSection[..1];
                    preRSection = preRSection[1..];
                    
                    // Part 4 - 123
                    part4 = preRSection[..3];
                }
                else if (secondChar == '0')
                {
                    // Second section is padded with 0, means the first section must not be
                    
                    // Part 3 - 1
                    part3 = "0" + preRSection[..1];
                    preRSection = preRSection[1..];
                    
                    // Part 4 - 123
                    part4 = preRSection[..3];
                }
                // 1/21/00, 1/22/01-06, 1/23/01-05, 1/24/01-05, 1/25/01-06
                else if (part1 == "1"
                    && part2 is "21" or "22" or "23" or "24" or "25")
                {
                    if (firstChar == '0')
                    {
                        // Part 3 - 1
                        part3 = preRSection[..2];
                        preRSection = preRSection[2..];

                        // Part 4 - 12
                        part4 = "0" + preRSection[..2];
                    }
                    else
                    {
                        // Part 3 - 1
                        part3 = "0" + preRSection[..1];
                        preRSection = preRSection[1..];

                        // Part 4 - 123
                        part4 = preRSection[..3];
                    }
                }
                // 2/27/19-29
                else if (part1 == "2" && part2 == "27")
                {
                    var first2Digits = int.Parse(preRSection[..2]);

                    if (first2Digits is >= 19 and <= 29)
                    {
                        // Part 3 - 12
                        part3 = preRSection[..2];
                        preRSection = preRSection[2..];

                        // Part 4 - 12
                        part4 = "0" + preRSection[..2];
                    }
                    else
                    {
                        // Part 3 - 1
                        part3 = "0" + preRSection[..1];
                        preRSection = preRSection[1..];

                        // Part 4 - 123
                        part4 = preRSection[..3];
                    }
                }
                // 2/26/30-34
                else if (part1 == "2" && part2 == "26")
                {
                    var first2Digits = int.Parse(preRSection[..2]);

                    if (first2Digits is >= 30 and <= 34)
                    {
                        // Part 3 - 12
                        part3 = preRSection[..2];
                        preRSection = preRSection[2..];

                        // Part 4 - 12
                        part4 = "0" + preRSection[..2];
                    }
                    else
                    {
                        // Part 3 - 1
                        part3 = "0" + preRSection[..1];
                        preRSection = preRSection[1..];

                        // Part 4 - 123
                        part4 = preRSection[..3];
                    }
                }
                // 2/27/1-18
                else if (part1 == "2" && part2 == "27")
                {
                    if (firstChar == '0')
                    {
                        // Part 3 - 12
                        part3 = preRSection[..2];
                        preRSection = preRSection[2..];

                        // Part 4 - 12
                        part4 = "0" + preRSection[..2];
                    }
                    else if (firstChar != '1')
                    {
                        // Part 3 - 1
                        part3 = "0" + preRSection[..1];
                        preRSection = preRSection[1..];

                        // Part 4 - 123
                        part4 = preRSection[..3];
                    }
                    else if (firstChar == '1')
                    {
                        // NOTE - This is a guess at this point, as there is no other way of doing it
                        
                        // Part 3 - 12
                        part3 = preRSection[..2];
                        preRSection = preRSection[2..];

                        // Part 4 - 12
                        part4 = "0" + preRSection[..2];
                    }
                    else
                    {
                        throw new Exception("Can't work it out (1)");
                    }
                }
                else
                {
                    throw new Exception("Can't work it out (2)");
                }
                
                parts.Add(part3);
                parts.Add(part4);
            }
            else if (preRSection.Length == 3)
            {
                // Part 3 - 1
                var part3 = "0" + preRSection[..1];
                preRSection = preRSection[1..];
                
                // Part 4 - 12
                var part4 = "0" + preRSection[..2];
                
                parts.Add(part3);
                parts.Add(part4);
            }
            
            // Part 5 (optional) - R01, RO2 etc...
            
            var postRSection = splitOnR.Length > 1 ? splitOnR[1] : null;
        
            if (!string.IsNullOrEmpty(postRSection))
            {
                parts.Add(postRSection);
            }
        }
        else if (remainingLicenceNumber[0] is 'n' or 'N')
        {
            // Part 1 - NE
            parts.Add(remainingLicenceNumber[..2]);
            remainingLicenceNumber = remainingLicenceNumber[2..];
            
            // Part 2 - 000
            parts.Add(remainingLicenceNumber[..3]);
            remainingLicenceNumber = remainingLicenceNumber[3..];

            if (remainingLicenceNumber.Length >= 7)
            {
                // Part 3 - 0000
                parts.Add(remainingLicenceNumber[..4]);
                remainingLicenceNumber = remainingLicenceNumber[4..];


                // Part 4 - 000
                parts.Add(remainingLicenceNumber[..3]);
                remainingLicenceNumber = remainingLicenceNumber[3..];
            }
            else
            {
                // Part 3 - 000
                parts.Add(remainingLicenceNumber[..3]);
                remainingLicenceNumber = remainingLicenceNumber[3..];


                // Part 4 - 000
                parts.Add(remainingLicenceNumber[..3]);
                remainingLicenceNumber = remainingLicenceNumber[3..];
            }

            // Part 5  Likely R01, but can be 1 and other stuff
            if (!string.IsNullOrEmpty(remainingLicenceNumber))
            {
                parts.Add(remainingLicenceNumber);
            }
        }
        
        return string.Join('/', parts);
    }
    
    private static bool IsNeLicenceNumber(string? licenceNumber)
    {
        if (string.IsNullOrEmpty(licenceNumber))
        {
            return false;
        }

        if (licenceNumber[0] is 'n' or 'N')
        {
            return true;
        }

        var firstThreeChars = licenceNumber[..3];
        return firstThreeChars is "121" or "122" or "123" or "124" or "125" or "226" or "227";
    }
    
    public static string? NoneSeperatedToNaldLicenceNumber(string? noneSeperatedLicenceNumber)
    {
        if (string.IsNullOrEmpty(noneSeperatedLicenceNumber))
        {
            return noneSeperatedLicenceNumber;
        }

        if (IsNeLicenceNumber(noneSeperatedLicenceNumber))
        {
            return ToFullLicenceNumber_NE(noneSeperatedLicenceNumber);
            //return Yorkshire1_ToNaldLicenceNumber(noneSeperatedLicenceNumber);
        }
        
        // TODO some other way
        return Yorkshire1_ToNaldLicenceNumber(noneSeperatedLicenceNumber);
    }

    public static string? PadLicenceNumber(string? licenceNumber)
    {
        if (string.IsNullOrEmpty(licenceNumber))
        {
            return licenceNumber;
        }
        
        if (IsNeLicenceNumber(licenceNumber))
        {
            return ToFullLicenceNumber_NE(licenceNumber);
        }

        licenceNumber = licenceNumber.Replace("//", "/");

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
        
        return NOTYorkshire1_PadLicenceNumber(licenceNumber);
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

    private static string? NOTYorkshire1_PadLicenceNumber(string? licenceNumber)
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