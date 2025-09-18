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
        @"([A-Z0-9]{1,3}[\/ .][0-9]{1,4}[\/ .][0-9]{1,4}([\/ .][0-9]{2,4}([\/ .][A-Z0-9]{3})?)?)|([A-Z0-9]{1,3}\/[A-Z0-9]{1,3})";
    
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

            if (line.Text.Contains("NE/026/0034/052"))
            {
                
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
                    
                    var containsSplitter = subLine.Contains(' ') || column.Text.Contains('/')|| column.Text.Contains('.');

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

                    var regexMatches = LicenceNumbersRegex().IsMatch(numberLine);
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