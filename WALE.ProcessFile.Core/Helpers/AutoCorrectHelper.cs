using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OutputSchema;
using WeCantSpell.Hunspell;

namespace WALE.ProcessFile.Core.Helpers;

public static class AutoCorrectHelper
{
    public static void RemoveSpacesAroundSlashes(IReadOnlyList<LineAndWords> returnLines)
    {
        foreach (var returnLine in returnLines)
        {
            if (returnLine.Text?.Contains('/') != true)
            {
                continue;
            }
            
            var originalText = returnLine.Text;
            var newText = RemoveSpacesAroundSlashes(returnLine.Text);

            if (newText == originalText)
            {
                continue;
            }
            
            var newWords = new List<DocumentLineWord?>();
            var wordTexts = newText!.Split(' ');

            foreach (var wordText in wordTexts)
            {
                var wordBefore = returnLine.Words!.FirstOrDefault(w => w!.Text == wordText)
                    ?? returnLine.Words!.FirstOrDefault(w => wordText.StartsWith(w!.Text))
                    ?? returnLine.Words!.FirstOrDefault(w => w!.Text.StartsWith(wordText))
                    ?? returnLine.Words!.First();             

                var newWord = new DocumentLineWord(
                    wordText,
                    wordBefore!.OcrConfidence,
                    wordBefore.Coordinates,
                    wordBefore.HandwrittenOrTyped)
                {
                    Autocorrected = true
                };

                newWords.Add(newWord);
            }

            returnLine.Words = newWords;
        }
    }
    
    public static void RemoveSpacesAroundSlashes(IReadOnlyList<DocumentLine> returnLines)
    {
        foreach (var returnLine in returnLines)
        {
            foreach (var column in returnLine.Columns)
            {
                if (column.Text.Contains('/') != true)
                {
                    continue;
                }

                var originalText = column.Text;
                var newText = RemoveSpacesAroundSlashes(column.Text);

                if (newText == originalText)
                {
                    continue;
                }

                var newWords = new List<DocumentLineWord>();
                var wordTexts = newText!.Split(' ');

                foreach (var wordText in wordTexts)
                {
                    var wordBefore = column.Words.FirstOrDefault(x => x.Text == wordText)
                        ?? column.Words.FirstOrDefault(w => wordText.StartsWith(w.Text));

                    var newWord = new DocumentLineWord(
                        wordText,
                        wordBefore!.OcrConfidence,
                        wordBefore.Coordinates,
                        wordBefore.HandwrittenOrTyped)
                    {
                        Autocorrected = true
                    };

                    newWords.Add(newWord);
                }

                column.Words = newWords;
            }
        }
    }

    private static string? RemoveSpacesAroundSlashes(string? wordText)
    {
        if (wordText == null)
        {
            return null;
        }
        
        if (wordText.Contains("/ 0")) wordText = wordText.Replace("/ 0", "/0");
        if (wordText.Contains("/ 1")) wordText = wordText.Replace("/ 1", "/1");
        if (wordText.Contains("/ 2")) wordText = wordText.Replace("/ 2", "/2");
        if (wordText.Contains("/ 3")) wordText = wordText.Replace("/ 3", "/3");
        if (wordText.Contains("/ 4")) wordText = wordText.Replace("/ 4", "/4");
        if (wordText.Contains("/ 5")) wordText = wordText.Replace("/ 5", "/5");
        if (wordText.Contains("/ 6")) wordText = wordText.Replace("/ 6", "/6");
        if (wordText.Contains("/ 7")) wordText = wordText.Replace("/ 7", "/7");
        if (wordText.Contains("/ 8")) wordText = wordText.Replace("/ 8", "/8");
        if (wordText.Contains("/ 9")) wordText = wordText.Replace("/ 9", "/9");
        if (wordText.Contains("0 /")) wordText = wordText.Replace("0 /", "0/");
        if (wordText.Contains("1 /")) wordText = wordText.Replace("1 /", "1/");
        if (wordText.Contains("2 /")) wordText = wordText.Replace("2 /", "2/");
        if (wordText.Contains("3 /")) wordText = wordText.Replace("3 /", "3/");
        if (wordText.Contains("4 /")) wordText = wordText.Replace("4 /", "4/");
        if (wordText.Contains("5 /")) wordText = wordText.Replace("5 /", "5/");
        if (wordText.Contains("6 /")) wordText = wordText.Replace("6 /", "6/");
        if (wordText.Contains("7 /")) wordText = wordText.Replace("7 /", "7/");
        if (wordText.Contains("8 /")) wordText = wordText.Replace("8 /", "8/");
        if (wordText.Contains("9 /")) wordText = wordText.Replace("9 /", "9/");

        return wordText;
    }
    
    public static DocumentLineWord? ReplaceSomeSpecialCharacters(DocumentLineWord? word)
    {
        if (word == null)
        {
            return null;
        }
        
        var wordText = word.Text;

        while (wordText.Contains(" .")) // TODO can this ever ben found at a word level?
        {
            wordText = wordText.Replace(" .", ".");
        }
        
        while (wordText.Contains(".."))
        {
            wordText = wordText.Replace("..", ".");
        }
        
        if (wordText.Contains("ᵗʰ"))
        {
            wordText = wordText.Replace("ᵗʰ", "th");
        }

        word.Text = wordText!.Trim();

        if (word.Text == ".")
        {
            return null;
        }
        
        return word;
    }

    public static DocumentLineWord? AutoCorrectWordIfNecessary(DocumentLineWord? word)
    {
        if (word == null)
        {
            return null;
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
        
        const int minLengthForAutocorrection = 2;
        const int maxConfidenceNotToFix = 63;
        const int maxLengthDifference = 3;

        // No matter the confidence, these are wrong
        if (word.Text.Contains("fallons", StringComparison.InvariantCultureIgnoreCase)
            || word.Text.Contains("pallons", StringComparison.InvariantCultureIgnoreCase))
        {
            word.Text = "gallons";
            word.Autocorrected = true;
            
            return word;
        }
        
        if (word.Text.Contains("dayof", StringComparison.InvariantCultureIgnoreCase))
        {
            word.Text = "day of";
            word.Autocorrected = true;
            
            return word;
        }
        
        if (word.Text.Contains("MARCI", StringComparison.InvariantCultureIgnoreCase))
        {
            word.Text = "march";
            word.Autocorrected = true;
            
            return word;
        }
        
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

    public static async Task<List<DocumentLineWord>> AutoCorrectTextAsync(
        List<DocumentLineWord> lineWords,
        bool removeFirstWordIfLowercase,
        bool checkDictionary)
    {
        var firstWord = lineWords.FirstOrDefault();
        
        if (CompanyNameHelper.StartsWithCompanyOrPersonalPrefix(firstWord?.Text)
            || CompanyNameHelper.CompanyWords.Any(companyWord => firstWord?.Text.StartsWith(companyWord) ?? false))
        {
            return lineWords;
        }
        
        var words = lineWords
            .Select((word, index) =>
            (
                word,
                lineWords.Count > index + 1 ? lineWords[index + 1] : null
            ))
            .ToList();

        var newWords = new List<DocumentLineWord>();
        var isFirstWord = true;
        var skipNextWord = false;
        
        foreach (var (word, nextWord) in words)
        {
            if (skipNextWord)
            {
                skipNextWord = false;
                continue;
            }

            var wordText = word.Text;
            
            if (isFirstWord)
            {
                isFirstWord = false;

                if (removeFirstWordIfLowercase
                    && wordText.Length >= 1
                    && char.IsLower(wordText[0])
                    && !CompanyNameHelper.CompanyWords.Any(companyWord =>
                        companyWord.Contains($"{wordText} ", StringComparison.InvariantCultureIgnoreCase)))
                {
                    continue;
                }
            }

            const string esqTitle = "esq";
            
            // TO DO make more generic
            if (wordText.Equals(esqTitle, StringComparison.InvariantCultureIgnoreCase))
            {
                newWords.Add(word);
                continue;
            }
            
            if (words.Count >= 2)
            {
                if (CompanyNameHelper.MayBeInitials(wordText))
                {
                    newWords.Add(word);                       
                    continue;
                }

                var wordKnownToBeSpeltCorrectly = true;

                if (checkDictionary)
                {
                    wordKnownToBeSpeltCorrectly = CustomDictionary.Check(wordText) || Dictionary.Check(wordText);
                }
                
                var nextWordText = nextWord?.Text;
                
                if (
                    !string.IsNullOrWhiteSpace(nextWordText)
                    && (wordText.Length > 1 || nextWordText.Length > 1)
                    && wordText.All(char.IsLetterOrDigit)
                    && nextWordText.All(char.IsLetterOrDigit)
                    && (wordText.Length == 1 || !wordKnownToBeSpeltCorrectly))
                {
                    var removedSpaceCombinedWord = $"{word.Text}{nextWord?.Text}";
                    
                    if (checkDictionary)
                    {
                        if (CustomDictionary.Check(removedSpaceCombinedWord)
                            || Dictionary.Check(removedSpaceCombinedWord))
                        {
                            var currentWordCloned = word.Clone();
                            currentWordCloned.Text = removedSpaceCombinedWord;
                            currentWordCloned.Autocorrected = true;
                            
                            newWords.Add(currentWordCloned);
                            skipNextWord = true;

                            continue;
                        }
                    }
                }

                var containsSymbol = !wordText.All(char.IsLetterOrDigit);
                
                if (wordText.Length <= 1 || containsSymbol || wordText.Split('.').Length >= 3)
                {
                    newWords.Add(word);                       
                    continue;
                }

                if (CommonMisspellings.TryGetValue(wordText, out var fixedMispellingValue))
                {
                    var currentWordCloned = word.Clone();
                    currentWordCloned.Text = fixedMispellingValue;
                    currentWordCloned.Autocorrected = true;
                    
                    newWords.Add(currentWordCloned);
                    continue;
                }
                
                if (!checkDictionary)
                {
                    newWords.Add(word);                       
                    continue;
                }

                string? topSuggestion;

                if (!wordKnownToBeSpeltCorrectly
                    && word.OcrConfidence < 90
                    && !string.IsNullOrEmpty(topSuggestion = GetTopSuggestion(wordText)))
                {
                    if (topSuggestion.Equals($"{wordText}s", StringComparison.InvariantCultureIgnoreCase)
                        || $"{topSuggestion}s".Equals(wordText, StringComparison.InvariantCultureIgnoreCase))
                    {
                        newWords.Add(word);
                        continue;
                    }
                    
                    var currentWordCloned = word.Clone();
                    currentWordCloned.Text = topSuggestion;
                    currentWordCloned.Autocorrected = true;
                    
                    newWords.Add(currentWordCloned);
                    continue;
                }
            }

            newWords.Add(word);
        }

        return newWords;
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
        
        // Too different from the original
        if (IsTotallyDifferentWord(word, topSuggestion))
        {
            return null;
        }
        
        var allUppercase = word.All(char.IsUpper);
        return allUppercase ? topSuggestion.ToUpper() : topSuggestion;
    }

    private static bool IsTotallyDifferentWord(string oldWord, string newWord)
    {
        var differenceCount = 0;
        
        var oldWordLower = oldWord.ToLower();
        var newWordLower = newWord.ToLower();

        var c = 0;
        foreach (var newWordChar in newWordLower)
        {
            var isAddition = !oldWordLower.Contains(newWordChar);

            if (isAddition)
            {
                differenceCount += 1;
            }
            else
            {
                if (c++ == 0 && newWordChar != oldWordLower[0])
                {
                    differenceCount += 1;
                }
            }
        }
        
        foreach (var oldWordChar in oldWordLower)
        {
            var removedLetter = !newWordLower.Contains(oldWordChar);

            if (removedLetter)
            {
                differenceCount += 1;
            }
        }
        
        // TODO - something with the shared character order
        
        var bothWordLength = newWordLower.Length + oldWordLower.Length;
        var differencePercent = (int)(100.0 / bothWordLength) * differenceCount;

        if (differencePercent >= 33)
        {
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    ///     Calculate the difference between 2 strings using the Levenshtein distance algorithm.
    /// From https://gist.github.com/Davidblkx/e12ab0bb2aff7fd8072632b396538560
    /// </summary>
    /// <param name="source1">First string</param>
    /// <param name="source2">Second string</param>
    /// <returns></returns>
    private static int Calculate(string source1, string source2) //O(n*m)
    {
        var source1Length = source1.Length;
        var source2Length = source2.Length;

        var matrix = new int[source1Length + 1, source2Length + 1];

        // First calculation, if one entry is empty return full length
        if (source1Length == 0)
            return source2Length;

        if (source2Length == 0)
            return source1Length;

        // Initialization of matrix with row size source1Length and columns size source2Length
        for (var i = 0; i <= source1Length; matrix[i, 0] = i++){}
        for (var j = 0; j <= source2Length; matrix[0, j] = j++){}

        // Calculate rows and collumns distances
        for (var i = 1; i <= source1Length; i++)
        {
            for (var j = 1; j <= source2Length; j++)
            {
                var cost = (source2[j - 1] == source1[i - 1]) ? 0 : 1;

                matrix[i, j] = Math.Min(
                    Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                    matrix[i - 1, j - 1] + cost);
            }
        }
        // return result
        return matrix[source1Length, source2Length];
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
        "Authority",
        "per"
    ];
    
    public static readonly WordList CustomDictionary = WordList.CreateFromWords(CustomSuggestions);
}