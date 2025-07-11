using System.Globalization;
using System.Text.RegularExpressions;
using CsvHelper;
using WALE.ProcessFile.Services.Enums;
using WALE.ProcessFile.Services.Models;
using WeCantSpell.Hunspell;

namespace WALE.ProcessFile.Services.Helpers;

public static partial class DataHelpers
{
    private static readonly WordList Dictionary = WordList.CreateFromFiles("en_GB.dic");

    public static string GetFilenameWithoutExtensions(string pdfFilePath)
    {
        var filenameParts = pdfFilePath.Split('/').Last().Split('.');
        return string.Join('-', filenameParts.Take(filenameParts.Length - 1));
    }
    
    private static HashSet<string>? _firstNamesCsv { get; set; }

    private static HashSet<string> FirstNamesCsv
    {
        get
        {
            if (_firstNamesCsv != null)
            {
                return _firstNamesCsv;
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

            _firstNamesCsv = returnList;
            return _firstNamesCsv;
        }
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
    
    public static IEnumerable<DocumentLine> TrimList(IEnumerable<DocumentLine> sourceList)
    {
        return sourceList
            .SkipWhile(x => string.IsNullOrWhiteSpace(x.Text))
            .Reverse()
            .SkipWhile(x => string.IsNullOrWhiteSpace(x.Text))
            .Reverse()
            .ToList();
    }

    public static string? TrimFormatting(string? text)
    {
        var trimmed = text?.Trim();

        while (trimmed?.Length >= 1
            && (char.IsPunctuation(trimmed[0])
                || char.IsSymbol(trimmed[0])
                || char.IsWhiteSpace(trimmed[0])))
        {
            trimmed = trimmed[1..];
        }
        
        while (trimmed?.Length >= 1
            && trimmed[^1] != ')'
            && (char.IsPunctuation(trimmed[^1])
                || char.IsSymbol(trimmed[^1])
                || char.IsWhiteSpace(trimmed[^1])))
        {
            trimmed = trimmed[..^1];
        }

        return trimmed;
    }

    public static string Standardise(string text)
    {
        return text
            .Trim()
            .Replace("‘‘", "\"")
            .Replace("’’", "\"")            
            .Replace("‘", "'")
            .Replace("’", "'")
            .Replace("“", "\"")
            .Replace("”", "\"")
            .Replace("'\"", "\"")
            .Replace("'", "\"")
            .Replace("\u00b0", "*") // degree character, OCR thinks it sees it for some small text
            .Replace("  ", " ")
            .Replace("\"\"", "\"");
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
    
    [GeneratedRegex(@"[a-zA-Z]\d[a-zA-Z]")]
    private static partial Regex CharDigitCharRegex();

    [GeneratedRegex(@"[0-9A-Z]{1,2}\/[0-9]{1,5}(\/[0-9\.A-Z\*]{1,4}\/\d{1,4})*")]
    private static partial Regex LicenceNumbersSlashesRegex();

    [GeneratedRegex(@"[0-9A-Z]{1,2} [0-9]{1,5}( [0-9\.A-Z\*]{1,4} \d{1,4})*")]
    private static partial Regex LicenceNumbersSpacesRegex();    
    
    public static bool IsCorruptedText(string? line)
    {
        if (string.IsNullOrEmpty(line))
        {
            return false;
        }
        
        var containsSpecialChar = line
            .Replace(" ", string.Empty)
            .Replace("/", string.Empty)
            .Replace(".", string.Empty)
            .Replace("(", string.Empty)
            .Replace(")", string.Empty)            
            .Replace(",", string.Empty)
            .Replace("\"", string.Empty)
            .Replace("-", string.Empty) 
            .Replace("*", string.Empty)            
            .Any(ch => !char.IsLetterOrDigit(ch));

        if (line.Length < 8 && CharDigitCharRegex().IsMatch(line))
        {
            return true;
        }
        
        if (char.IsLower(line[0]) && containsSpecialChar)
        {
            return true;
        }
        
        if (StartsWithCompanyOrPersonalPrefix(line))
        {
            return false;
        }

        if (EndsWithCompanyOrPersonalSuffix(line))
        {
            return false;
        }
        
        var wordsSplit = line.Split(' ');
        var countOfVeryShortWordsOrSymbols = wordsSplit
            .Count(word =>
                word.Length <= 2 
                && !word.Any(char.IsDigit) 
                && word.ToLower() != "a"
                && word.ToLower() != "a,"
                && word.ToLower() != "b,"
                && word.ToLower() != "c,"
                && word.ToLower() != "d,"
                && word.ToLower() != "e,"                
                && word.ToLower() != "of"
                && word.ToLower() != "to"
                && word.ToLower() != "be");

        var percentagePerWord = 100.0 / wordsSplit.Length;
        
        var percentageOfShortWords = countOfVeryShortWordsOrSymbols * percentagePerWord;
        var percentageOfSuspectedIncorrectWords = wordsSplit.Count(word => 
                !Dictionary.Check(word)
                && !word.Contains('/')
                && !double.TryParse(word.Replace("TL", string.Empty).Replace(",", string.Empty), out _)
            ) * percentagePerWord;
        
        return (countOfVeryShortWordsOrSymbols > 3 && percentageOfShortWords >= 20.0)
            || (wordsSplit.Length >= 2 && percentageOfSuspectedIncorrectWords > 50);
    }

    public static bool AnyIsLicenceNumber(
        IEnumerable<DocumentLine?> lines,
        LabelToMatch label,
        out List<DocumentLine> numberLines)
    {
        numberLines = [];
        var anyIsMatch = false;
        
        foreach (var line in lines)
        {
            if (string.IsNullOrEmpty(line?.Text))
            {
                continue;
            }
            
            if (IsCorruptedText(line.Text))
            {
                continue;
            }

            var subLines = line.Text
                .Replace(" and", ",")
                .Replace(" for", ",")
                .Replace(" shall", ",")
                .Replace(" under", ",")
                .Replace(" from", ",")
                .Replace(" (", ",")                
                .Split(',');

            foreach (var subLine in subLines)
            {
                var invalid = subLine.Any(character =>
                    !char.IsLetter(character)
                    && !char.IsNumber(character)
                    && character != ' '
                    && character != '/'
                    && character != '.'
                    && character != '*');

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
                            numberLines.Add(new DocumentLine(
                                numberLine.Trim(),
                                line.LineNumber,
                                line.PageNumber,
                                line.Words.ToList()
                            ));
                                
                            anyIsMatch = true;
                        }

                        if (label.Multiple == MultipleType.False)
                        {
                            return anyIsMatch;                            
                        }
                    }
                    /*else if (false && !containsSplitter && long.TryParse(line.Text, out _)) // No licence numbers we need so far without a splitter
                    {
                        numberLines.Add(new DocumentLine(
                            line.Text.Trim(),
                            line.LineNumber,
                            line.PageNumber,
                            line.Words.ToList()
                        ));
                        
                        anyIsMatch = true;
                    }*/
                }
            }
        }
        
        return anyIsMatch;
    }

    public static bool AnyIsNumber(
        IEnumerable<DocumentLine?> lines,
        out DocumentLine? numberLine)
    {
        numberLine = null;

        var matched = false;
        var returnLines = new List<double>();

        var ls = lines.ToList();
        
        var lineNumber = ls.FirstOrDefault()?.LineNumber ?? -1;
        var pageNumber = ls.FirstOrDefault()?.PageNumber ?? -1;
        var lineWords = new List<DocumentLineWord>();
        
        foreach (var line in ls)
        {
            if (IsCorruptedText(line?.Text))
            {
                if (matched)
                {
                    break;
                }
                
                continue;
            }

            foreach (var word in line!.Text.Split(' '))
            {
                if (!double.TryParse(word, out var numberLineDbl))
                {
                    if (matched)
                    {
                        lineNumber = line.LineNumber;
                        pageNumber = line.PageNumber;
                        lineWords = line.Words;
                        
                        break;
                    }

                    continue;
                }

                returnLines.Add(numberLineDbl);
                matched = true;

                break;
            }
            
            /*if (matched) -- 2025/02/03 This only made a difference to 1 unit test and having it was negative to that test
            {
                break;
            }*/
        }

        if (returnLines.Count > 0)
        {
            var tempLine = returnLines.First(); // TODO something maybe relies on the following -  .MaxBy(text => text);
            
            numberLine = new DocumentLine(
                tempLine.ToString(CultureInfo.InvariantCulture),
                lineNumber,
                pageNumber,
                lineWords);
        }
        
        return matched;
    }

    public static bool AnyIsCompanyOrPersonalName(
        IEnumerable<DocumentLine?> lines,
        bool isPrevious,
        bool isOcr,
        out IReadOnlyList<DocumentLine>? companyOrPersonalNames)
    {
        companyOrPersonalNames = null;
        var returnList = new List<DocumentLine>();
        
        var matched = false;
        var returnLines = new List<string>();
        
        var lineNumber = -1;
        var pageNumber = -1;
        var lineWords = new List<DocumentLineWord>();
        
        foreach (var line in lines)
        {
            if (IsCorruptedText(line?.Text))
            {
                if (matched)
                {
                    break;
                }
                
                continue;
            }
            
            var correctedLine = isOcr ? new DocumentLine(
                AutoCorrectText(line!, true)!,
                line!.LineNumber,
                line.PageNumber,
                line.Words.ToList()) : line;

            correctedLine = new DocumentLine(
                TrimFormatting(correctedLine?.Text)!,
                correctedLine!.LineNumber,
                correctedLine.PageNumber,
                correctedLine.Words.ToList());

            if (IsCorruptedText(line?.Text))
            {
                if (matched) break;
                continue;
            }

            if (!TryGetCompanyOrPersonalName(correctedLine, out var companyOrPersonalName))
            {
                if (matched) break;
                continue;
            }

            correctedLine = new DocumentLine(
                companyOrPersonalName!,
                correctedLine.LineNumber,
                correctedLine.PageNumber,
                correctedLine.Words.ToList());
            
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
                lineWords = correctedLine.Words;
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
                    lineWords)));
        }

        if (returnList.Count > 0)
        {
            companyOrPersonalNames = returnList;
        }
        
        return matched;
    }
    
    public static string? AutoCorrectText(DocumentLine? text, bool removeFirstWordIfLowercase)
    {
        if (StartsWithCompanyOrPersonalPrefix(text?.Text))
        {
            return text?.Text;
        }
        
        var wordsSplit = text?.Text.Split(' ');
        
        if (wordsSplit == null)
        {
            return null;
        }
        
        var words = wordsSplit
            .Select((line, index) =>
            (
                Standardise(line),
                wordsSplit.Length > index + 1 ? wordsSplit[index + 1] : null
            ))
            .ToList();

        var newWords = new List<string>();
        var isFirstWord = true;
        var skipNextWord = false;

        foreach (var (word, nextWord) in words)
        {
            if (skipNextWord)
            {
                skipNextWord = false;
                continue;
            }
            
            if (isFirstWord)
            {
                isFirstWord = false;

                if (removeFirstWordIfLowercase && word.Length > 0 && char.IsLower(word[0]))
                {
                    continue;
                }
            }

            // TO DO make more generic
            if (word.Equals("esq", StringComparison.InvariantCultureIgnoreCase))
            {
                newWords.Add(word);
                continue;
            }
            
            if (words.Count >= 2)
            {
                if (MayBeInitials(word))
                {
                    newWords.Add(word);                       
                    continue;
                }
                
                if (
                    (word.Length == 1 || !Dictionary.Check(word))
                    && !string.IsNullOrWhiteSpace(nextWord)
                    && (word.Length > 1 || nextWord.Length > 1))
                {
                    var removedSpaceCombinedWord = $"{word}{nextWord}";

                    if (Dictionary.Check(removedSpaceCombinedWord))
                    {
                        newWords.Add(removedSpaceCombinedWord);
                        skipNextWord = true;

                        continue;
                    }
                }

                if (word.Length <= 1 || word.Split(".").Length >= 3)
                {
                    newWords.Add(word);                       
                    continue;
                }
                
                var suggestions = Dictionary.Suggest(word).ToList();
                var topSuggestion = suggestions.FirstOrDefault(
                    suggestion => PreferredSuggestions.Contains(
                        suggestion,
                        StringComparer.InvariantCultureIgnoreCase)) ?? suggestions.FirstOrDefault();

                var shouldUseSuggestion =
                    PreferredSuggestions.Contains(topSuggestion,
                        StringComparer.InvariantCultureIgnoreCase)
                    || !Dictionary.Check(word);

                if (shouldUseSuggestion && !string.IsNullOrEmpty(topSuggestion))
                {
                    if (topSuggestion.Equals($"{word}s", StringComparison.InvariantCultureIgnoreCase)
                        || $"{topSuggestion}s".Equals(word, StringComparison.InvariantCultureIgnoreCase))
                    {
                        newWords.Add(word);
                        continue;
                    }
                    
                    newWords.Add(topSuggestion);
                    continue;
                }
            }

            newWords.Add(word);
        }

        return string.Join(" ", newWords);
    }

    private static bool MayBeInitials(string word)
    {
        return word.Length == 2 && word.All(char.IsUpper);
    }

    private static IEnumerable<string> PreferredSuggestions =>
    [
        "Mid",
        "Central",
        "North",
        "South",
        "Ltd",
        "Farm",
        "Farms"
    ];

    public static bool TryGetNumber(
        string? text,
        int lineNumber,
        int pageNumber,
        out DocumentLine? number)
    {
        number = null;
        
        if (text == null)
        {
            return false;
        }
        
        var IRRELEVANT_WORDS = new List<DocumentLineWord>();

        var list = text
            .Split(' ')
            .Select(result => new DocumentLine(
                result,
                lineNumber,
                pageNumber,
            IRRELEVANT_WORDS));
        
        if (AnyIsNumber(list, out var numberLine))
        {
            number = numberLine;
            return true;
        }

        return false;
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
    
    public static bool TryGetCompanyOrPersonalName(
        DocumentLine? text,
        out string? companyOrPersonalName)
    {
        companyOrPersonalName = null;
        
        if (text == null)
        {
            return false;
        }

        // TODO - bit of a hack
        if (ContainsDescriptionOfAgency(text.Text))
        {
            return false;
        }

        var parts = text.Text.Split(' ');
        var looksLikeNameWithInitials = parts.Length is 2 or 3 or 4
            && parts.First().Length is 1 or 2
            && parts.First().All(char.IsLetter)
            && (parts.Length == 2 || (parts[1].Length is 1 or 2 && parts[1].All(char.IsLetter)))
            && parts.Last().Length >= 3
            && parts.Last().All(char.IsLetter)
            && !parts.All(word => Dictionary.Check(word) && word.Length > 1);

        if (looksLikeNameWithInitials)
        {
            companyOrPersonalName = text.Text;            
            return true;
        }
        
        var containsDelimitter = ContainsCompanyOrPersonalSuffixDelimitter(
            text.Text,
            out var delimiter);
        
        if (StartsWithCompanyOrPersonalPrefix(text.Text)
            || ContainsCompanyOrPersonalWord(text.Text)
            || containsDelimitter)
        {
            if (EndsWithNoneCommpanyOrPersonalSuffix(text.Text))
            {
                return false;
            }
            
            if (containsDelimitter)
            {
                text.Text = text.Text[..(text.Text.IndexOf(delimiter!,
                    StringComparison.InvariantCultureIgnoreCase) + delimiter!.Length)];
            }
            
            companyOrPersonalName = text.Text;
            return true;
        }

        return false;
    }

    private static bool EndsWithNoneCommpanyOrPersonalSuffix(string? text)
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
    
    private static bool StartsWithCompanyOrPersonalPrefix(string? text)
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
    
    private static bool EndsWithCompanyOrPersonalSuffix(string? text)
    {
        if (text == null)
        {
            return false;
        }
        
        return CompanySuffixes
            .Any(suffix => text.EndsWith(suffix,
                StringComparison.InvariantCultureIgnoreCase));
    }    

    private static bool ContainsCompanyOrPersonalWord(string? text)
    {
        if (text == null)
        {
            return false;
        }
    
        var companyWords = new List<string>
        {
            "trading as"
        };

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
        
        return companyWords
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
}