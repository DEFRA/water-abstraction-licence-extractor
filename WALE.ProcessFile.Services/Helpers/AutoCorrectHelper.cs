using WALE.ProcessFile.Services.Formats;

namespace WALE.ProcessFile.Services.Helpers;

public static class AutoCorrectHelper
{
    public static string? AutoCorrectText(string? lineText, bool removeFirstWordIfLowercase)
    {
        if (CompanyName.StartsWithCompanyOrPersonalPrefix(lineText)
            || CompanyName.CompanyWords.Any(companyWord => lineText?.StartsWith(companyWord) ?? false))
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

            // TO DO make more generic
            if (word.Equals("esq", StringComparison.InvariantCultureIgnoreCase))
            {
                newWords.Add(word);
                continue;
            }
            
            if (words.Count >= 2)
            {
                if (CompanyName.MayBeInitials(word))
                {
                    newWords.Add(word);                       
                    continue;
                }
                
                if (
                    (word.Length == 1 || !DataHelper.Dictionary.Check(word))
                    && !string.IsNullOrWhiteSpace(nextWord)
                    && (word.Length > 1 || nextWord.Length > 1)
                    && word.All(char.IsLetterOrDigit)
                    && nextWord.All(char.IsLetterOrDigit))
                {
                    var removedSpaceCombinedWord = $"{word}{nextWord}";

                    if (DataHelper.Dictionary.Check(removedSpaceCombinedWord))
                    {
                        newWords.Add(removedSpaceCombinedWord);
                        skipNextWord = true;

                        continue;
                    }
                }

                var containsSymbol = !word.All(char.IsLetterOrDigit);
                
                if (word.Length <= 1 || containsSymbol || word.Split(".").Length >= 3)
                {
                    newWords.Add(word);                       
                    continue;
                }
                
                var suggestions = DataHelper.Dictionary.Suggest(word).ToList();
                var topSuggestion = suggestions.FirstOrDefault(
                    suggestion => PreferredSuggestions.Contains(
                        suggestion,
                        StringComparer.InvariantCultureIgnoreCase)) ?? suggestions.FirstOrDefault();

                var shouldUseSuggestion =
                    PreferredSuggestions.Contains(topSuggestion,
                        StringComparer.InvariantCultureIgnoreCase)
                    || !DataHelper.Dictionary.Check(word);

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
    
    private static readonly IEnumerable<string> PreferredSuggestions =
    [
        "Mid",
        "Central",
        "North",
        "South",
        "Ltd",
        "Farm",
        "Farms"
    ];
}