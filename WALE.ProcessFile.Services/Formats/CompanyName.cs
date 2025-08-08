using System.Globalization;
using CsvHelper;
using WALE.ProcessFile.Services.Constants;
using WALE.ProcessFile.Services.Helpers;
using WALE.ProcessFile.Services.Models;

namespace WALE.ProcessFile.Services.Formats;

public static class CompanyName
{
    public const string Constant = "CompanyName";

    public static List<string> CompanyWords => ["trading as"];

    public static bool AnyIsCompanyOrPersonalName(
        IEnumerable<DocumentLine?> lines,
        LabelToMatch label,
        bool isPrevious,
        bool isOcr,
        out IReadOnlyList<DocumentLine>? matchedLines)
    {
        matchedLines = null;
        var returnList = new List<DocumentLine>();
        
        var matched = false;
        var returnLines = new List<string>();
        
        var lineNumber = -1;
        var pageNumber = -1;
        var lineColumns = new List<DocumentLineColumn>();
        
        foreach (var line in lines)
        {
            if (LabelMatchingHelper.TextContainsForbiddenResult(line, label))
            {
                continue;
            }
            
            if (DataHelper.IsCorruptedText(line?.Text))
            {
                if (matched)
                {
                    break;
                }
                
                continue;
            }
            
            var correctedLine = isOcr
                ? line!.Clone(AutoCorrectHelper.AutoCorrectText(line, true)!)
                : line;

            correctedLine = correctedLine!.Clone(FormattingHelper.TrimFormatting(correctedLine.Text)!);

            if (DataHelper.IsCorruptedText(line?.Text)
                || !TryGetCompanyOrPersonalName(correctedLine, label, out var companyOrPersonalName))
            {
                if (matched)
                {
                    break;
                }

                continue;
            }

            correctedLine = correctedLine.Clone(companyOrPersonalName!);
            
            // It's only the company suffix with nothing else
            if (CompanySuffixes.Any(companySuffix =>
                companySuffix.Trim().Equals(correctedLine.Text, StringComparison.InvariantCultureIgnoreCase)))
            {
                if (matched) break;
                continue;
            }

            if (lineNumber == -1)
            {
                lineNumber = correctedLine.LineNumber;
                pageNumber = correctedLine.PageNumber;
                lineColumns = correctedLine.Columns;
            }

            returnLines.Add(correctedLine.Text);
            matched = true;
            
            if (ContainsCompanyOrPersonalSuffixDelimitter(correctedLine.Text, out _))
            {
                break;
            }
        }

        if (isPrevious)
        {
            returnLines.Reverse();
        }
        
        if (returnLines.Count > 0)
        {
            if (returnLines.Count > 1)
            {
                returnLines.Remove("The Environment Agency");
            }

            var newReturnLines = new List<string>();

            foreach (var returnLine in returnLines)
            {
                if (ContainsCompanyOrPersonalSuffixDelimitter(returnLine, out _))
                {
                    newReturnLines.Add(returnLine);
                    break;
                }
                
                newReturnLines.Add(returnLine);
            }

            returnLines = newReturnLines;
            returnList.AddRange(returnLines.Select(returnLine =>
                new DocumentLine(
                    returnLine,
                    lineNumber,
                    pageNumber,
                    lineColumns,
                    PositionConstants.UnknownCoordinate,
                    PositionConstants.UnknownCoordinate,
                    PositionConstants.UnknownCoordinate)));
        }

        if (returnList.Count > 0)
        {
            matchedLines = returnList;
        }
        
        return matched;
    }
    
    public static bool TryGetCompanyOrPersonalName(
        DocumentLine? line,
        LabelToMatch label,
        out string? matchedCompanyOrPersonalName)
    {
        matchedCompanyOrPersonalName = null;
        
        if (line == null)
        {
            return false;
        }
        
        if (LabelMatchingHelper.TextContainsForbiddenResult(line, label))
        {
            return false;
        }
        
        // TODO - bit of a hack
        if (ContainsDescriptionOfAgency(line.Text))
        {
            return false;
        }

        var parts = line.Text.Split(' ');
        var looksLikeNameWithInitials = parts.Length is 2 or 3 or 4
            && parts.First().Length is 1 or 2
            && parts.First().All(char.IsLetter)
            && (parts.Length == 2 || (parts[1].Length is 1 or 2 && parts[1].All(char.IsLetter)))
            && parts.Last().Length >= 3
            && parts.Last().All(char.IsLetter)
            && !parts.All(word => DataHelper.Dictionary.Check(word) && word.Length > 1);

        if (looksLikeNameWithInitials && !line.Text.Contains('"'))
        {
            matchedCompanyOrPersonalName = line.Text;            
            return true;
        }
        
        var containsDelimitter = ContainsCompanyOrPersonalSuffixDelimitter(
            line.Text,
            out var delimiter);
        
        if (StartsWithCompanyOrPersonalPrefix(line.Text)
            || ContainsCompanyOrPersonalWord(line.Text)
            || containsDelimitter)
        {
            if (EndsWithNoneCompanyOrPersonalSuffix(line.Text))
            {
                return false;
            }
            
            if (containsDelimitter)
            {
                line.Text = line.Text[..(line.Text.IndexOf(delimiter!,
                    StringComparison.InvariantCultureIgnoreCase) + delimiter!.Length)];
            }
            
            matchedCompanyOrPersonalName = line.Text;
            return true;
        }

        return false;
    }
    
    public static bool StartsWithCompanyOrPersonalPrefix(string? text)
    {
        if (text == null)
        {
            return false;
        }
    
        var prefixes = new List<string>
        {
            "department ",
            "university ",
            "mr ",
            "mr. ",
            "mrs ",
            "mrs. ",
            "miss ",
            "miss. ",
            "lord ",
            "lord. ",
            "lady ",
            "lady. "            
        };
        
        return prefixes
            .Any(prefix => text.StartsWith(prefix,
                StringComparison.InvariantCultureIgnoreCase));
    }
    
    public static bool EndsWithCompanyOrPersonalSuffix(string? text)
    {
        if (text == null)
        {
            return false;
        }
        
        return CompanySuffixes
            .Any(suffix => text.EndsWith(suffix,
                StringComparison.InvariantCultureIgnoreCase));
    }
    
    public static bool MayBeInitials(string word)
    {
        return word.Length == 2 && word.All(char.IsUpper);
    }
    
    public static bool ContainsCompanyOrPersonalWord(string? text)
    {
        if (text == null)
        {
            return false;
        }

        var textParts = text.Split(' ');
        var secondWordString = textParts.Length >= 2 ? text[textParts[0].Length..].Trim() : null;
        
        foreach (var name in FirstNamesCsv)
        {
            if (text.StartsWith($"{name} ", StringComparison.InvariantCultureIgnoreCase)
                || secondWordString?.StartsWith($"{name} ", StringComparison.InvariantCultureIgnoreCase) == true)
            {
                return true;
            }
        }
        
        return CompanyWords
            .Any(companyWord => text.Contains(companyWord,
                StringComparison.InvariantCultureIgnoreCase));
    }
    
    public static bool ContainsCompanyOrPersonalSuffixDelimitter(
        string? text,
        out string? delimiter)
    {
        delimiter = null;
        
        if (text == null)
        {
            return false;
        }

        string? delimiterLoop = null;
        var found = CompanySuffixes
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

        return text.Contains("hereinafter", StringComparison.InvariantCultureIgnoreCase)
               || text.Contains("grants this", StringComparison.InvariantCultureIgnoreCase)
               || text.Contains("a agency", StringComparison.InvariantCultureIgnoreCase);
    }
    
    private static bool EndsWithNoneCompanyOrPersonalSuffix(string? text)
    {
        if (text == null)
        {
            return false;
        }
        
        var suffixes = new List<string>
        {
            " road",
            " lane",
            " avenue",
            " street"            
        };
        
        
        return suffixes
            .Any(suffix => text.EndsWith(suffix,
                StringComparison.InvariantCultureIgnoreCase)
                || char.IsDigit(text.Last()));
    }
    
    private static readonly List<string> CompanySuffixes =
    [
        " agency",
        " limited",
        " charities",
        " ltd",
        " plc",
        " school",
        " corporation",
        " university",
        " and sons",
        " water board",
        " users",
        " estate",
        " quarry",
        " nurseries",
        " esq.", // Personal suffix
        " esq",
        " and son",
        " and partners",
        " farms"
    ];
    
    private static HashSet<string>? firstNamesCsv { get; set; }

    private static HashSet<string> FirstNamesCsv
    {
        get
        {
            if (firstNamesCsv != null)
            {
                return firstNamesCsv;
            }

            var avoidWords = new List<string>
            {
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
                "thames" // River
            };

            var returnList = new HashSet<string>();

            using var reader = new StreamReader("Data/first-names.csv");
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            
            var records = csv.GetRecords<FirstNamesRow>().ToList();
                
            foreach (var name in records.Select(record => record.FirstForename))
            {
                if (avoidWords.Contains(name!.ToLower())
                    || name.Length <= 2)
                {
                    continue;
                }
                
                returnList.Add(name);
            }

            firstNamesCsv = returnList;
            return firstNamesCsv;
        }
    }
}