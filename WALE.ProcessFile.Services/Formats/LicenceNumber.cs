using System.Text.RegularExpressions;
using WALE.ProcessFile.Services.Enums;
using WALE.ProcessFile.Services.Helpers;
using WALE.ProcessFile.Services.Models;

namespace WALE.ProcessFile.Services.Formats;

public static partial class LicenceNumber
{
    public const string Constant = "LicenceNumber";

    // AA/123, AA/123/123, AA/123/123/123, AA 123 123 123 or AA.123.123.123 (and some other variations of this)
    public const string RegexPatten =
        @"([A-Z0-9]{1,3}[\/ .][A-Z0-9]{1,5}([\/ .][0-9]{1,4})?([\/ .][0-9A-Z\*]{1,4})?([\/ .][0-9]{1,4})?([\/ .][0-9A-Z]{1,3})?[\/ .]?)|([A-Z0-9]{1,3}\/[A-Z0-9]{1,3})";
    
    public static bool AnyIsLicenceNumber(
        IEnumerable<DocumentLine?> lines,
        LabelToMatch label,
        out List<DocumentLine> matchedLines)
    {
        matchedLines = [];
        var anyMatchFound = false;
        var findSingleResult = label.MultipleBehaviour is MultipleBehaviour.FindSingleInstanceOfLabelWithASingleValue;
        
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
                    var containsSplitter = subLine.Contains(' ')
                       || column.Text.Contains('/')
                       || column.Text.Contains('.');

                    if (!containsSplitter || subLine.Length < 4)
                    {
                        continue;
                    }
                    
                    var numberLine = subLine;

                    if (numberLine.Contains('/'))
                    {
                        numberLine = numberLine.Replace(" ", string.Empty);
                    }
                    
                    var enoughPartsWithNumbers = numberLine
                        .Replace(" ", "/")
                        .Replace(".", "/")
                        .Split('/')
                        .Count(p => p.Any(char.IsDigit)) >= 2;

                    var regexMatches = LicenceNumbersRegex().Matches(numberLine);
                    var isMatch = regexMatches.Count >= 1 && enoughPartsWithNumbers;

                    if (!isMatch)
                    {
                        continue;
                    }

                    var value = regexMatches[0].Value;
                    var hasInvalidComboOfSeperators = (value.Contains('.') && value.Contains(' '))                                               
                        || (value.Contains('/') && value.Contains(' '));
                    
                    if (hasInvalidComboOfSeperators)
                    {
                        continue;
                    }
                    
                    var colText = FormattingHelper.TrimFormatting(
                        value,
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

                if (!anyMatchFoundForColumn)
                {
                    newColumns.Add(column);
                }
            }

            if (!anyMatchFoundForLine)
            {
                continue;
            }

            if (findSingleResult)
            {
                return anyMatchFound;
            }
        }
        
        return anyMatchFound;
    }
    
    [GeneratedRegex(RegexPatten)]
    private static partial Regex LicenceNumbersRegex();
}