using WALE.ProcessFile.Core.Models;
using WeCantSpell.Hunspell;

namespace WALE.ProcessFile.Core.Helpers;

public static class AutoCorrectHelper
{
    public static DocumentLineWord? ReplaceSomeSpecialCharacters(DocumentLineWord? word)
    {
        if (word == null)
        {
            return null;
        }
        
        var wordText = word.Text;

        if (wordText.Contains("ᵗʰ"))
        {
            wordText = wordText.Replace("ᵗʰ", "th");
        }

        word.Text = wordText;
        return word;
    }

    public static DocumentLineWord? AutoCorrectWordIfNecessary(DocumentLineWord? word)
    {
        if (word == null)
        {
            return null;
        }
        
        if (word.Text.StartsWith("Authority", StringComparison.InvariantCultureIgnoreCase))
        {
            
        }
        
        var wordTextWithoutPunctuation = word.Text;

        if (wordTextWithoutPunctuation.Contains(','))
        {
            wordTextWithoutPunctuation = wordTextWithoutPunctuation.Replace(",", string.Empty);
        }
        
        if (wordTextWithoutPunctuation.Contains('.'))
        {
            wordTextWithoutPunctuation = wordTextWithoutPunctuation.Replace(".", string.Empty);
        }
        
        if (wordTextWithoutPunctuation.Contains('\''))
        {
            wordTextWithoutPunctuation = wordTextWithoutPunctuation.Replace("'", string.Empty);
        }
        
        if (wordTextWithoutPunctuation.Contains('"'))
        {
            wordTextWithoutPunctuation = wordTextWithoutPunctuation.Replace("\"", string.Empty);
        }

        if (word.Text.Equals("ld", StringComparison.InvariantCultureIgnoreCase))
        {
            
        }

        const int minLengthForAutocorrection = 2;
        const int maxConfidenceNotToFix = 50;
        const int maxLengthDifference = 3;
        
        if (word is { OcrConfidence: < maxConfidenceNotToFix, Text.Length: >= minLengthForAutocorrection }
            && wordTextWithoutPunctuation.Count(char.IsAsciiLetter) >= minLengthForAutocorrection
            && !CustomDictionary.Check(wordTextWithoutPunctuation)
            && !Dictionary.Check(wordTextWithoutPunctuation))
        {
            var topSuggestion = GetTopSuggestion(wordTextWithoutPunctuation);

            if (topSuggestion == null)
            {
                return word;
            }
            
            var lengthDiff = Math.Abs(topSuggestion.Length - wordTextWithoutPunctuation.Length);

            if (lengthDiff <= maxLengthDifference)
            {
                word.Text = topSuggestion;
                word.Autocorrected = true;
                
                return word;
            }
        }
        
        return word;
    }

    public static string? AutoCorrectText(
        string? lineText,
        bool removeFirstWordIfLowercase,
        bool checkDictionary)
    {
        if (CompanyNameHelper.StartsWithCompanyOrPersonalPrefix(lineText)
            || CompanyNameHelper.CompanyWords.Any(companyWord => lineText?.StartsWith(companyWord) ?? false))
        {
            return lineText;
        }
        
        var wordsSplit = lineText?.Split(' ');
        
        if (wordsSplit == null)
        {
            return null;
        }
        
        var words = wordsSplit
            .Select((line, index) =>
            (
                line,
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

            const string esqTitle = "esq";
            
            // TO DO make more generic
            if (word.Equals(esqTitle, StringComparison.InvariantCultureIgnoreCase))
            {
                newWords.Add(word);
                continue;
            }
            
            if (words.Count >= 2)
            {
                if (CompanyNameHelper.MayBeInitials(word))
                {
                    newWords.Add(word);                       
                    continue;
                }

                var wordSpeltCorrectly = !checkDictionary || CustomDictionary.Check(word) || Dictionary.Check(word);
                
                if (
                    !string.IsNullOrWhiteSpace(nextWord)
                    && (word.Length > 1 || nextWord.Length > 1)
                    && word.All(char.IsLetterOrDigit)
                    && nextWord.All(char.IsLetterOrDigit)
                    && (word.Length == 1 || !wordSpeltCorrectly))
                {
                    var removedSpaceCombinedWord = $"{word}{nextWord}";

                    if (checkDictionary && (CustomDictionary.Check(removedSpaceCombinedWord) || Dictionary.Check(removedSpaceCombinedWord)))
                    {
                        newWords.Add(removedSpaceCombinedWord);
                        skipNextWord = true;

                        continue;
                    }
                }

                var containsSymbol = !word.All(char.IsLetterOrDigit);
                
                if (word.Length <= 1 || containsSymbol || word.Split('.').Length >= 3)
                {
                    newWords.Add(word);                       
                    continue;
                }

                if (CommonMisspellings.TryGetValue(word, out var value))
                {
                    newWords.Add(value);
                    continue;
                }
                
                if (!checkDictionary)
                {
                    newWords.Add(word);                       
                    continue;
                }
                
                var topSuggestion = GetTopSuggestion(word);

                if (!wordSpeltCorrectly && !string.IsNullOrEmpty(topSuggestion))
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

    private static readonly Dictionary<string, string> CommonMisspellings = new()
    {
        { "nid", "mid" },
        { "Nid", "Mid" },
        { "NID", "MID" },
        { "forms", "farms" },
        { "Forms", "Farms" },
        { "FORMS", "FARMS" },
        { "fallons", "gallons" },
        { "Fallons", "Gallons" },
        { "FALLONS", "GALLONS" },
        { "pallons", "gallons" },
        { "Pallons", "Gallons" },
        { "PALLONS", "GALLONS" },
        { "ld", "ltd" },
        { "Ld", "Ltd" },
        { "LD", "LTD" }
    };

    public static string? GetTopSuggestion(string word)
    {
        var customSuggestions = CustomDictionary.Suggest(
            word,
            new QueryOptions
            {
                MaxSuggestions = 1,
                MaxSharps = 0,
                MaxWords = 1,
                MaxCompoundSuggestions = 0
            }).ToList();
        
        var allUppercase = word.All(char.IsUpper);

        if (!customSuggestions.Any())
        {
            if (word.Contains('w', StringComparison.InvariantCultureIgnoreCase))
            {
                var letterMSwappedWithW = word
                    .Replace('w', 'm')
                    .Replace('W', 'M');
                
                return GetTopSuggestion(letterMSwappedWithW);
            }
            
            if (word.Contains('q', StringComparison.InvariantCultureIgnoreCase))
            {
                var letterQSwappedWithG = word
                    .Replace('q', 'g')
                    .Replace('Q', 'G');
                
                return GetTopSuggestion(letterQSwappedWithG);
            }
            
            return null;
        }
        
        var topSuggestion = customSuggestions.First();
        return allUppercase ? topSuggestion.ToUpper() : topSuggestion;
    }
    
    public static readonly WordList Dictionary = WordList.CreateFromFiles("en_GB.dic");
 
    private static readonly IEnumerable<string> CustomSuggestions =
    [
        "Cheshire",
        "Mid",
        "Central",
        "North",
        "South",
        "Ltd",
        "Farm",
        "Farms",
        "Gallons",
        "August",
        "Aug",
        "March",
        "Dated",
        "Authority"
    ];
    
    public static readonly WordList CustomDictionary = WordList.CreateFromWords(CustomSuggestions);
}