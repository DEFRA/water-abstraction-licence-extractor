using System.Globalization;
using CsvHelper;
using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Services.Models;

namespace WALE.ProcessFile.Services.Formats;

public static class CompanyName
{
    public const string Constant = "CompanyName";

    public static async Task<(bool, IReadOnlyList<DocumentLine>)> AnyIsCompanyOrPersonalNameAsync(
        IEnumerable<DocumentLine?> lines,
        LabelToMatch label,
        bool lineNumbersAreDescending,
        bool isOcr,
        LookupConfiguration? lookupConfiguration)
    {
        // TODO get rid of any dates in here (d/m/yy)
        
        var matchedLines = new List<DocumentLine>();
        var matched = false;
        
        var initialMatchedLines = new List<DocumentLine>();
        
        foreach (var line in lines)
        {
            if (line == null)
            {
                continue;
            }
            
            var anyLineMatch = false;
            var newColumns = new List<DocumentLineColumn>();
            
            foreach (var column in line.Columns)
            {
                if (LabelMatchingHelper.ShouldSkipResultAsForbidden(column.Text, label))
                {
                    newColumns.Add(column);
                    continue;
                }
            
                if (DataHelper.IsCorruptedText(column.Text, isOcr))
                {
                    newColumns.Add(column);
                    
                    if (matched)
                    {
                        break;
                    }
                
                    continue;
                }
                
                var text = FormattingHelper.TrimFormatting(column.Text, true, true)!;

                // For speed, first check without dictionary
                var correctedText = isOcr
                    ? await AutoCorrectHelper.AutoCorrectTextAsync(text, true, false)
                    : text;
                
                if (DataHelper.IsCorruptedText(correctedText, isOcr)
                    || !TryGetCompanyOrPersonalName(correctedText, label, lookupConfiguration, out var companyOrPersonalName))
                {
                    if (matched)
                    {
                        break;
                    }

                    continue;
                }
                
                correctedText = isOcr
                    ? await AutoCorrectHelper.AutoCorrectTextAsync(correctedText, true, label.AutoCorrect)
                    : text;
            
                if (!TryGetCompanyOrPersonalName(correctedText, label, lookupConfiguration, out companyOrPersonalName))
                {
                    if (matched)
                    {
                        break;
                    }

                    continue;
                }
                
                // It's only the company suffix with nothing else
                if (CompanyNameHelper.CompanySuffixes.Any(companySuffix =>
                        companySuffix.Trim().Equals(companyOrPersonalName, StringComparison.InvariantCultureIgnoreCase)))
                {
                    newColumns.Add(column);

                    if (matched)
                    {
                        break;
                    }
                    
                    continue;
                }

                var clonedColumn = new DocumentLineColumn(companyOrPersonalName!);
                newColumns.Add(clonedColumn);

                anyLineMatch = true;
                matched = true;
            
                if (ContainsCompanyOrPersonalSuffixDelimitter(clonedColumn.Text, out _))
                {
                    break;
                }   
            }

            if (!anyLineMatch)
            {
                continue;
            }
            
            var clonedLine = line.Clone(newColumns);
            initialMatchedLines.Add(clonedLine);
        }

        if (lineNumbersAreDescending)
        {
            initialMatchedLines.Reverse();
        }
        
        var returnList = new List<DocumentLine>();
        
        if (initialMatchedLines.Count > 0)
        {
            if (initialMatchedLines.Count > 1)
            {
                const string theEA = "The Environment Agency";
                
                var eaLinePos = initialMatchedLines
                    .FindIndex(x => x.Text.Contains(theEA));

                if (eaLinePos > -1)
                {
                    initialMatchedLines.RemoveAt(eaLinePos);   
                }
            }

            var newReturnLines = new List<DocumentLine>();

            foreach (var returnLine in initialMatchedLines)
            {
                if (ContainsCompanyOrPersonalSuffixDelimitter(returnLine.Text, out _))
                {
                    newReturnLines.Add(returnLine);
                    break;
                }
                
                newReturnLines.Add(returnLine);
            }

            initialMatchedLines = newReturnLines;
            returnList.AddRange(initialMatchedLines);
        }

        if (returnList.Count > 0)
        {
            matchedLines = returnList;
        }
        
        return (matched, matchedLines);
    }
    
    public static bool TryGetCompanyOrPersonalName(
        string? lineText,
        LabelToMatch label,
        LookupConfiguration? lookupConfiguration,
        out string? matchedCompanyOrPersonalName)
    {
        matchedCompanyOrPersonalName = null;
        
        if (lineText == null)
        {
            return false;
        }
        
        if (LabelMatchingHelper.ShouldSkipResultAsForbidden(lineText, label))
        {
            return false;
        }
        
        // TODO - bit of a hack
        if (ContainsDescriptionOfAgency(lineText))
        {
            return false;
        }

        var parts = lineText.Split(' ');
        var looksLikeNameWithInitials = parts.Length >= 2
            && parts.First().Length is 1 or 2
            && parts.First().All(char.IsLetter)
            && (parts.Length == 2 || (parts[1].Length is 1 or 2 && parts[1].All(char.IsLetter)))
            && parts.Last().Length >= 3
            && parts.Last().All(char.IsLetter)
            && !parts.All(word => word.Length > 1
                && (!label.AutoCorrect || AutoCorrectHelper.CustomDictionary.Check(word) || AutoCorrectHelper.Dictionary.Check(word)));

        if (looksLikeNameWithInitials && !lineText.Contains('"'))
        {
            matchedCompanyOrPersonalName = lineText;
            return true;
        }
        
        var containsDelimitter = ContainsCompanyOrPersonalSuffixDelimitter(
            lineText,
            out var delimiter);
        
        if (CompanyNameHelper.StartsWithCompanyOrPersonalPrefix(lineText)
            || ContainsCompanyOrPersonalWord(lineText, lookupConfiguration!.ValidLowercaseFirstNames)
            || containsDelimitter)
        {
            if (EndsWithNoneCompanyOrPersonalSuffix(lineText))
            {
                return false;
            }
            
            var text = lineText;
            
            if (containsDelimitter)
            {
                text = text[..(text.IndexOf(delimiter!,
                    StringComparison.InvariantCultureIgnoreCase) + delimiter!.Length)];
            }
            
            matchedCompanyOrPersonalName = text;

            if (Date.ContainsDate(matchedCompanyOrPersonalName, out var dates))
            {
                foreach (var date in dates)
                {
                    matchedCompanyOrPersonalName = matchedCompanyOrPersonalName.Replace(date, string.Empty);
                }

                matchedCompanyOrPersonalName = matchedCompanyOrPersonalName.Trim();
            }

            return true;
        }

        return false;
    }
    
    private static bool ContainsCompanyOrPersonalWord(string? text, HashSet<string> firstNamesCsv)
    {
        if (text == null)
        {
            return false;
        }

        var textParts = text.Split(' ');
        var secondWordString = textParts.Length >= 2 ? text[textParts[0].Length..].Trim() : null;
        
        foreach (var name in firstNamesCsv)
        {
            if (text.StartsWith($"{name} ", StringComparison.InvariantCultureIgnoreCase)
                || secondWordString?.StartsWith($"{name} ", StringComparison.InvariantCultureIgnoreCase) == true)
            {
                return true;
            }
        }
        
        return CompanyNameHelper.CompanyWords
            .Any(companyWord => text.Contains(companyWord,
                StringComparison.InvariantCultureIgnoreCase));
    }
    
    private static bool ContainsCompanyOrPersonalSuffixDelimitter(
        string? text,
        out string? delimiter)
    {
        delimiter = null;
        
        if (text == null)
        {
            return false;
        }

        string? delimiterLoop = null;
        var found = CompanyNameHelper.CompanySuffixes
            .Any(companySuffix =>
            {
                var contains = text.Contains(companySuffix,
                    StringComparison.InvariantCultureIgnoreCase);

                if (contains)
                {
                    delimiterLoop = companySuffix;
                }

                return contains;
            });

        delimiter = delimiterLoop;
        return found;
    }
    
    private static bool ContainsDescriptionOfAgency(string? text)
    {
        if (text == null)
        {
            return false;
        }

        const string hereinafter = "hereinafter";
        const string grantsthis = "grants this";
        const string aagency = "a agency";

        return text.Contains(hereinafter, StringComparison.InvariantCultureIgnoreCase)
               || text.Contains(grantsthis, StringComparison.InvariantCultureIgnoreCase)
               || text.Contains(aagency, StringComparison.InvariantCultureIgnoreCase);
    }
    
    private static bool EndsWithNoneCompanyOrPersonalSuffix(string? text)
    {
        if (text == null)
        {
            return false;
        }
        
        return Suffixes
            .Any(suffix => text.EndsWith(suffix,
                StringComparison.InvariantCultureIgnoreCase)
                || char.IsDigit(text.Last()));
    }
    
    public static async Task<HashSet<string>> GetFirstNamesCsvFromFileAsync()
    {
        var returnList = new HashSet<string>();
        var dtStart = DateTime.Now;
        
        using var reader = new StreamReader("Data/first-names.csv");
        using var csv = new CsvReader(reader, new CultureInfo("en-GB"));
        
        var data = csv.GetRecordsAsync<FirstNamesRow>();
        
        await foreach (var record in data)
        {
            var name = record.FirstForename!.ToLower();
            if (name.Length <= 2 || FirstNameAvoidWords.Contains(name))
            {
                continue;
            }
            
            returnList.Add(name);
        }

        ConsoleHelper.WriteLine($"INFO - {nameof(CompanyName)} - Loading FirstNamesCsv took {(DateTime.Now - dtStart).TotalMilliseconds}ms");
        return returnList;
    }
    
    private static readonly List<string> Suffixes =
    [
        " road",
        " lane",
        " avenue",
        " street"
    ];
    
    private static readonly HashSet<string> FirstNameAvoidWords =
    [
        "the", // Too generic
        "po", // PO box
        "mersey", // Geography
        "june", // Month
        "charity", // Company word
        "grant", // Legal word
        "manor", // house name,
        "red", // color, not common name
        "south", // direction
        "north", // direction
        "west", // direction
        "rho", // In postcodes
        "rivers", // water
        "see", // doing word,
        "heh", // Is it a name?
        "you", //  Is it a name?
        "thames", // River
        "fee"
    ];
}