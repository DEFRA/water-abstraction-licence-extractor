using System.Text.RegularExpressions;
using WALE.ProcessFile.Models;
using WALE.ProcessFile.Models.Enums;
using WALE.ProcessFile.Services.Helpers;

namespace WALE.ProcessFile.Services.Formats;

public static partial class LicenceNumber
{
    public const string Constant = "LicenceNumber";

    // AA/123, AA/123/123, AA/123/123/123, 'AA 123 123 123' or AA.123.123.123 (and some other variations of this)
    public const string YorkshireRegexPatten =
        @"([A-Z0-9]{1,3}[\/ .][A-Z0-9]{1,5}([\/ .][0-9]{1,4})?([\/ .][0-9A-Z\*]{1,4})?([\/ .][0-9]{1,4})?([\/ .][0-9A-Z]{1,3})?[\/ .]?)|([A-Z0-9]{1,3}\/[A-Z0-9]{1,3})";

    private static readonly string[] PrefixesToExclude =
    [
        "NT ",
        "NU ",
        "NY ",
        "NGR "
    ];
    
    public static bool AnyIsLicenceNumber(
        IEnumerable<DocumentLine?> lines,
        LabelToMatch label,
        bool isOcr,
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

                var columnText = column.Text;

                if (columnText.Contains(" and"))
                {
                    columnText = columnText.Replace(" and", splitChar);
                }
                
                if (columnText.Contains(" for"))
                {
                    columnText = columnText.Replace(" for", splitChar);
                }
                
                if (columnText.Contains(" shall"))
                {
                    columnText = columnText.Replace(" shall", splitChar);
                }
                
                if (columnText.Contains(" under"))
                {
                    columnText = columnText.Replace(" under", splitChar);
                }
                
                if (columnText.Contains(" from"))
                {
                    columnText = columnText.Replace(" from", splitChar);
                }
                
                if (columnText.Contains(" ("))
                {
                    columnText = columnText.Replace(" (", splitChar);
                }
                
                var subLines = columnText.Split(splitChar);

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

                    if (isOcr && numberLine.Contains('/') && numberLine.Contains(' '))
                    {
                        numberLine = numberLine.Replace(" ", string.Empty);
                    }

                    var regexMatches = LicenceNumbersRegex().Matches(numberLine);
                    var isMatch = regexMatches.Count >= 1;
                        
                    if (!isMatch)
                    {
                        continue;
                    }

                    var numberLineWithSlashes = numberLine;

                    if (numberLineWithSlashes.Contains(' '))
                    {
                        numberLineWithSlashes = numberLineWithSlashes.Replace(" ", "/");
                    }
                    
                    if (numberLineWithSlashes.Contains('.'))
                    {
                        numberLineWithSlashes = numberLineWithSlashes.Replace(".", "/");
                    }
                    
                    var enoughPartsWithNumbers = numberLineWithSlashes
                        .Split('/')
                        .Count(section => section.Any(char.IsDigit)) >= 2;
                    
                    isMatch = enoughPartsWithNumbers;

                    if (!isMatch)
                    {
                        continue;
                    }

                    var value = regexMatches[0].Value;
                    var hasInvalidComboOfSeperators = (value.Contains('.') && value.Contains(' '))
                        || (value.Contains('/') && value.Contains(' '));
                        //|| (value.Contains('/') && value.Contains('.')) -- This combination is valid e.g. 11/42/28.2/7
                    
                    if (hasInvalidComboOfSeperators)
                    {
                        continue;
                    }

                    var shortLimit = value.Contains('/') ? 5 : 6;
                    var veryShort = value.Length < shortLimit;
                    if (veryShort)
                    {
                        continue;
                    }
                    
                    var totalDigits = value.Count(char.IsDigit);

                    if (totalDigits < 4)
                    {
                        continue;
                    }

                    var isPostcode = value.Length == 7 || value.Length == 8
                        && char.IsUpper(value[0])
                        && value.Count(c => c == ' ') == 1
                        && value.Split(' ')[1].Length == 3;
                            
                    if (isPostcode)
                    {
                        continue;
                    }
                    
                    var atLeastOneDigit = value.Any(char.IsDigit);
                    if (!atLeastOneDigit)
                    {
                        continue;
                    }

                    var isOsRef = (value.StartsWith('S') || value.StartsWith('T'))
                        && value[2] == ' '
                        && value.All(c => c != '/')
                        && value.All(c => c != '.');

                    if (!isOsRef)
                    {
                        isOsRef = value.Contains("NZ ") || value.Contains(" NZ");
                    }
                    
                    if (isOsRef)
                    {
                        continue;
                    }
                    
                    var noCharSlashOrDot = value.All(c => c != '/')
                        && value.All(c => c != '.')
                        && !value.Any(char.IsLetter);

                    if (noCharSlashOrDot && value.Split(' ') .Length < 3)
                    {
                        continue;
                    }

                    var excludedPrefixFound = PrefixesToExclude.Any(prefixToExclude =>
                        value.StartsWith(prefixToExclude));

                    if (excludedPrefixFound)
                    {
                        continue;
                    }
                    
                    var colText = FormattingHelper.TrimFormatting(
                        value,
                        true,
                        true);
                        
                    var clonedColumn = new DocumentLineColumn(colText!);
                    newColumns.Clear();
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
    
    [GeneratedRegex(YorkshireRegexPatten)]
    private static partial Regex LicenceNumbersRegex();
}