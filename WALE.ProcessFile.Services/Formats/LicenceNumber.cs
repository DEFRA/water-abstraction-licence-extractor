using System.Text.RegularExpressions;
using WALE.ProcessFile.Services.Enums;
using WALE.ProcessFile.Services.Helpers;
using WALE.ProcessFile.Services.Models;

namespace WALE.ProcessFile.Services.Formats;

public static partial class LicenceNumber
{
    public const string Constant = "LicenceNumber";
    
    public static bool AnyIsLicenceNumber(
        IEnumerable<DocumentLine?> lines,
        LabelToMatch label,
        out List<DocumentLine> matchedLines)
    {
        matchedLines = [];
        var anyIsMatch = false;
        
        foreach (var line in lines)
        {
            if (string.IsNullOrEmpty(line?.Text))
            {
                continue;
            }

            if (!line.Text.Any(char.IsDigit))
            {
                continue;
            }
            
            if (DataHelper.IsCorruptedText(line.Text))
            {
                continue;
            }

            const string splitChar = ",";

            var subLines = line.Text
                .Replace(" and", splitChar)
                .Replace(" for", splitChar)
                .Replace(" shall", splitChar)
                .Replace(" under", splitChar)
                .Replace(" from", splitChar)
                .Replace(" (", splitChar)                
                .Split(splitChar);

            foreach (var subLine in subLines)
            {
                var invalid = subLine.Any(character =>
                    !char.IsLetter(character)
                    && !char.IsNumber(character)
                    && character != ' '
                    && character != '/'
                    && character != '.'
                    && character != '*');

                var words = subLine.Split(' ');

                foreach (var word in words)
                {
                    if (word.Length >= 3 && word.All(char.IsLetter) && DataHelper.Dictionary.Check(word))
                    {
                        invalid = true;
                        break;
                    }
                }                
                
                if (!invalid)
                {
                    var containsSplitter = subLine.Contains(' ') || line.Text.Contains('/');

                    if (containsSplitter && subLine.Length >= 4)
                    {
                        var numberLine = subLine;

                        if (numberLine.Contains('/'))
                        {
                            numberLine = numberLine.Replace(" ", string.Empty);
                        }

                        var regexMatches = LicenceNumbersSlashesRegex().IsMatch(numberLine)
                            || LicenceNumbersSpacesRegex().IsMatch(numberLine);

                        var enoughPartsWithNumbers = numberLine
                           .Replace(" ", "/")
                           .Split('/')
                           .Count(p => p.Any(char.IsDigit)) >= 2;

                        var match = regexMatches && enoughPartsWithNumbers;
                        
                        if (match)
                        {
                            matchedLines.Add(line.Clone(numberLine.Trim()));
                            anyIsMatch = true;
                        }

                        if (label.Multiple == MultipleType.False)
                        {
                            return anyIsMatch;                            
                        }
                    }
                }
            }
        }
        
        return anyIsMatch;
    }
    
    [GeneratedRegex(@"[0-9A-Z]{1,2}\/[0-9]{1,5}(\/[0-9\.A-Z\*]{1,4}\/\d{1,4})*")]
    private static partial Regex LicenceNumbersSlashesRegex();

    [GeneratedRegex(@"[0-9A-Z]{1,2} [0-9]{1,5}( [0-9\.A-Z\*]{1,4} \d{1,4})*")]
    private static partial Regex LicenceNumbersSpacesRegex();    
}