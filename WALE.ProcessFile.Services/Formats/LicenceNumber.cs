using System.Text.RegularExpressions;
using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Services.Formats;

public static partial class LicenceNumber
{
    public const string Constant = "LicenceNumber";

    // AA/123, AA/123/123, AA/123/123/123, 'AA 123 123 123' or AA.123.123.123 (and some other variations of this)
    public const string YorkshireRegexPatten =
        @"([A-Z0-9]{1,3}[\/ .]{1,2}[A-Z0-9]{1,5}([\/ .]{1,2}[0-9]{1,4})?([\/ .]{0,2}[0-9A-Z\*]{1,4})?([\/ .]{1,2}[0-9]{1,4})?([\/ .]{1,2}[0-9A-Z]{1,3})?[\/ .]{0,2})|([A-Z0-9]{1,3}\/{1,2}[A-Z0-9]{1,3})";

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

                if (columnText.Contains(". "))
                {
                    columnText = columnText.Replace(". ", $"{splitChar} ");
                }
                
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
                
                var slashSpacePos = columnText.IndexOf("/ ", StringComparison.Ordinal);
                var isSlashSpaceDigit = slashSpacePos > 0
                    && columnText.Length > slashSpacePos + 2 
                    & char.IsDigit(columnText.Substring(slashSpacePos + 2, 1)[0]);

                if (isSlashSpaceDigit)
                {
                    columnText = columnText.Replace("/ ", "/");
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
                    
                    // It's a date
                    if (Date.IsDate(numberLine))
                    {
                        continue;
                    }
                    
                    var numberLineWithSlashes = numberLine;

                    // No slashes, 1 dot - is invalid format (its probably a decimal number
                    if (!numberLineWithSlashes.Contains('/') && numberLineWithSlashes.Count(c => c == '.') == 1)
                    {
                        continue;
                    }
                    
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

                    var value = regexMatches[0].Value.Trim();

                    if (subLine.Contains($"{value.Replace("/", ".")}m", StringComparison.InvariantCultureIgnoreCase))
                    {
                        continue;
                    }
                    
                    var lengthBeforePeriod = value.IndexOf(".", StringComparison.Ordinal);

                    if (lengthBeforePeriod >= 10)
                    {
                        value = value.Split('.')[0];
                    }
                    
                    var lengthBeforeSpace = value.IndexOf(" ", StringComparison.Ordinal);

                    if (lengthBeforeSpace >= 10)
                    {
                        value = value.Split(' ')[0];
                    }
                    
                    // It's a date (check again)
                    if (Date.IsDate(value))
                    {
                        continue;
                    }

                    var previousCharIsLetterCount = -1;
                    var maxSequenceLength = 0;

                    maxSequenceLength = value
                        .Select(c =>
                        {
                            if (c == ' ' || c == '/' || c == '.')
                            {
                                return maxSequenceLength;
                            }
                            
                            if (!char.IsLetter(c))
                            {
                                // ReSharper disable once AccessToModifiedClosure
                                if (previousCharIsLetterCount + 1 > maxSequenceLength)
                                {
                                    maxSequenceLength = previousCharIsLetterCount + 1;
                                }

                                previousCharIsLetterCount = -1;
                                return maxSequenceLength;
                            }

                            previousCharIsLetterCount += 1;

                            if (previousCharIsLetterCount + 1 > maxSequenceLength)
                            {
                                maxSequenceLength = previousCharIsLetterCount + 1;
                            }

                            return maxSequenceLength;
                        })
                        .OrderByDescending(r => r)
                        .First();
                    
                    if (maxSequenceLength >= 3)
                    {
                        continue;
                    }
                    
                    var hasInvalidComboOfSeperators = (value.Contains('.') && value.Contains(' '))
                        || (value.Contains('/') && value.Contains(' '));
                        //|| (value.Contains('/') && value.Contains('.')) -- This combination is valid e.g. 11/42/28.2/7

                    if (hasInvalidComboOfSeperators)
                    {
                        continue;
                    }
                    
                    // Its a value + unit
                    if (value.Contains('.') && (value.Contains("MI") || value.Contains("M3")))
                    {
                        continue;
                    }
                    
                    var sections = value.Split('/');

                    // Last bit is too long - its because of a space near the end
                    if (sections.Length == 4 && sections.Last().Length == 4)
                    {
                        var valueWithoutLastChar = value[..^1];
                        var valueEndingWithSpace = $"{valueWithoutLastChar} ";
                        
                        if (subLine.Contains(valueEndingWithSpace))
                        {
                            value = valueWithoutLastChar;
                        }
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

                    var isPostcode = (value.Length == 7 || value.Length == 8)
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
                        isOsRef =
                            value.StartsWith("NZ ")
                            || value.Contains(" NZ")
                            || value.StartsWith("TA ")
                            || value.Contains(" TA ")
                            || value.StartsWith("SE ")
                            || value.Contains(" SE ")
                            || value.StartsWith("TF ")
                            || value.Contains(" TF ")

                            || value.StartsWith("A ")
                            || value.StartsWith("B ")
                            || value.StartsWith("C ")
                            || value.StartsWith("D ")
                            || value.StartsWith("E ")
                            || value.StartsWith("F ")
                            || value.StartsWith("G ")
                            || value.StartsWith("H ")
                            || value.StartsWith("I ")
                            || value.StartsWith("J ")
                            || value.StartsWith("K ")
                            || value.StartsWith("L ")
                            || value.StartsWith("M ")
                            || value.StartsWith("N ")
                            || value.StartsWith("O ")
                            || value.StartsWith("P ")
                            || value.StartsWith("Q ")
                            || value.StartsWith("R ")
                            || value.StartsWith("S ")
                            || value.StartsWith("T ")
                            || value.StartsWith("U ")
                            || value.StartsWith("V ")
                            || value.StartsWith("W ")
                            || value.StartsWith("X ")
                            || value.StartsWith("Y ")
                            || value.StartsWith("Z ")
                            
                            || value.EndsWith(" A")
                            || value.EndsWith(" B")
                            || value.EndsWith(" C")
                            || value.EndsWith(" D")
                            || value.EndsWith(" E")
                            || value.EndsWith(" F")
                            || value.EndsWith(" G")
                            || value.EndsWith(" H")
                            || value.EndsWith(" I")
                            || value.EndsWith(" J")
                            || value.EndsWith(" K")
                            || value.EndsWith(" L")
                            || value.EndsWith(" M")
                            || value.EndsWith(" N")
                            || value.EndsWith(" O")
                            || value.EndsWith(" P")
                            || value.EndsWith(" Q")
                            || value.EndsWith(" R")
                            || value.EndsWith(" S")
                            || value.EndsWith(" T")
                            || value.EndsWith(" U")
                            || value.EndsWith(" V")
                            || value.EndsWith(" W")
                            || value.EndsWith(" X")
                            || value.EndsWith(" Y")
                            || value.EndsWith(" Z");
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
                    
                    // Invalid end of a licence number (probably cut off)
                    if (value.EndsWith("/R"))
                    {
                        continue;
                    }
                    
                    // Invalid end of a licence number
                    if (value.EndsWith("V", StringComparison.InvariantCultureIgnoreCase))
                    {
                        continue;
                    }

                    var colText = FormattingHelper.TrimFormatting(
                        value,
                        true,
                        true);

                    if (colText!.Contains("1/22/2/87"))
                    {
                        
                    }
                    
                    // It's part of something bigger (like a drawing reference e.g. '13/002-The...')
                    if (subLine.Contains($"{colText}-"))
                    {
                        continue;
                    }
                    
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