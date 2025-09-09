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
        var anyMatchFound = false;
        
        foreach (var line in lines)
        {
            if (line == null)
            {
                continue;
            }

            var anyMatchFoundForLine = false;
            var newColumns = new List<DocumentLineColumn>();
            
            foreach (var column in line.Columns)
            {
                var anyMatchFoundForColumn = false;
                
                if (string.IsNullOrEmpty(column.Text)
                    || !column.Text.Any(char.IsDigit)
                    || DataHelper.IsCorruptedText(column.Text))
                {
                    newColumns.Add(column);
                    continue;
                }

                const string splitChar = ",";

                var subLines = column.Text
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

                    if (words.Any(word => word.Length >= 3
                        && word.All(char.IsLetter)
                        && DataHelper.Dictionary.Check(word)))
                    {
                        invalid = true;
                    }

                    if (invalid)
                    {
                        continue;
                    }
                    
                    var containsSplitter = subLine.Contains(' ') || column.Text.Contains('/');

                    if (!containsSplitter || subLine.Length < 4)
                    {
                        continue;
                    }
                    
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
                        var colText = FormattingHelper.TrimFormatting(
                            numberLine,
                            true,
                            true);
                        
                        var clonedColumn = new DocumentLineColumn(colText!);
                        newColumns.Add(clonedColumn);

                        var clonedLine = line.Clone(newColumns);
                        matchedLines.Add(clonedLine);

                        newColumns = [];
                        anyMatchFoundForColumn = true;
                        anyMatchFoundForLine = true;
                        anyMatchFound = true;
                    }
                }

                if (!anyMatchFoundForColumn)
                {
                    newColumns.Add(column);
                }
            }

            if (!anyMatchFoundForLine)
            {
                continue;
            }

            if (label.Multiple is MultipleType.False)
            {
                return anyMatchFound;
            }
        }
        
        return anyMatchFound;
    }
    
    [GeneratedRegex(@"[0-9A-Z]{1,2}\/[0-9]{1,5}(\/[0-9\.A-Z\*]{1,4}\/\d{1,4})*")]
    private static partial Regex LicenceNumbersSlashesRegex();

    [GeneratedRegex(@"[0-9A-Z]{1,2} [0-9]{1,5}( [0-9\.A-Z\*]{1,4} \d{1,4})*")]
    private static partial Regex LicenceNumbersSpacesRegex();    
}