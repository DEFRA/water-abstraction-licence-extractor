using System.Text;
using System.Text.RegularExpressions;
using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Core.Helpers;

public static partial class DataHelper
{
    public static List<DocumentLine> RemoveExcludesAndNotContains(
        LabelToMatch label,
        IReadOnlyList<DocumentLine>? betweenText,
        bool removeNotContains,
        bool trimPunctuation,
        out bool isForbidden,
        out IReadOnlyList<string>? removesUsed)
    {
        removesUsed = null;
        isForbidden = false;
        
        var inputList = betweenText ?? new List<DocumentLine>();
        
        if ((label.Remove?.Any() != true && label.IgnoreMatchIfContains?.Any() != true) || betweenText == null)
        {
            return FormattingHelper.RemoveMultipleBlankLines(inputList);
        }

        var returnList = new List<DocumentLine>();
        var removesUsedList = new List<string>();
        
        foreach (var line in inputList)
        {
            _ = RemoveExcludes(
                label,
                line.Text,
                false,
                false,
                null,
                out var removesUsedLoopOuter);

            // The whole line wants removing
            if (removesUsedLoopOuter?.Contains(line.Text) == true)
            {
                continue;
            }

            var newColumns = new List<DocumentLineColumn>();
            
            foreach (var column in line.Columns)
            {
                column.Words = column.Words
                    .Select((w, idx) =>
                    {
                        w.Text = RemoveExcludes(
                            label,
                            w.Text,
                            false,
                            false,
                            idx,
                            out _);

                        return w;
                    })
                    .ToList();
                
                if (removeNotContains && LabelMatchingHelper.ShouldSkipResultAsForbidden(column.Text, label))
                {
                    isForbidden = true;
                    return [];
                }

                var isLastColumn = line.Columns.Last() == column;
                var alteredText = RemoveExcludes(
                    label,
                    column.Text,
                    isLastColumn && trimPunctuation,
                    isLastColumn && trimPunctuation,
                    null,
                    out var removesUsedLoop);

                var alteredTextWords = DocumentLineColumn.FilterWordsFromText(
                    column.Words,
                    alteredText,
                    false);
                
                var clonedColumn = new DocumentLineColumn(alteredTextWords);
                newColumns.Add(clonedColumn);

                if (removesUsedLoop != null)
                {
                    removesUsedList.AddRange(removesUsedLoop);
                }
            }
            
            if (removeNotContains && LabelMatchingHelper.ShouldSkipResultAsForbidden(line.Text, label))
            {
                isForbidden = true;
                return [];
            }
            
            var clonedLine = line.Clone(newColumns);
            returnList.Add(clonedLine);
        }

        removesUsed = removesUsedList;
        return FormattingHelper.RemoveMultipleBlankLines(returnList);
    }

    public static string GetTextBeforeAtAndAfterLabelAsSingleString(
        List<TextAndLabelAndPosition>? textBeforeAtAndAfterLabel,
        bool includeLabelText)
    {
        var beforeStuff = textBeforeAtAndAfterLabel!
            .Where(tuple =>
                (includeLabelText && tuple.Label?.Position == LabelPosition.LabelIsActuallyResult)
                    || tuple.Label?.Position is LabelPosition.LabelIsBeforeTextToFind
                        or LabelPosition.TextToFindIsBetweenLabels)
            .OrderBy(tuple =>
            {
                return tuple.Label?.Position switch
                {
                    LabelPosition.LabelIsAfterTextToFind => -2,
                    LabelPosition.LabelIsActuallyResult => -1,
                    LabelPosition.TextToFindIsBetweenLabels => 0,
                    LabelPosition.LabelIsBeforeTextToFind => 1,
                    _ => throw new ArgumentOutOfRangeException()
                };
            })
            .SelectMany(tuple => tuple.ColumnsText!)
            .ToArray();
        
        return string.Join(' ', beforeStuff);
    }
    
    public static string RemoveExcludes(
        LabelToMatch label,
        string betweenText,
        bool trimPunctuationStart,
        bool trimPunctuationEnd,
        int? individualWordLineIndex,
        out IReadOnlyList<string>? removesUsed)
    {
        removesUsed = null;
        
        if ((label.Remove?.Any() != true && label.IgnoreMatchIfContains?.Any() != true)
            || string.IsNullOrEmpty(betweenText))
        {
            return betweenText;
        }
        
        var returnStr = betweenText;
        var removesUsedList = new List<string>();

        if (label.Remove != null)
        {
            foreach (var textToMatch in label.Remove)
            {
                if (textToMatch.Regex != null)
                {
                    var match = textToMatch.Regex.Match(returnStr);
                    
                    if (match.Success)
                    {
                        returnStr = textToMatch.Regex.Replace(
                            match.Value,
                            string.Empty);

                        removesUsedList.Add(match.Value);
                    }

                    continue;
                }

                if (!returnStr.Contains(textToMatch.Text, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (textToMatch.ColumnMustStartWith || textToMatch.LineMustStartWith)
                {
                    if ((individualWordLineIndex != 0 && individualWordLineIndex != null)
                        || !returnStr.StartsWith(textToMatch.Text, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                        
                    if (textToMatch.ColumnMustHave2SequentialNumbers)
                    {
                        var words = returnStr.Split(' ');
                        var countOfNumbers = words.Count(word => int.TryParse(word, out _));

                        if (countOfNumbers >= 2)
                        {
                            if (returnStr.Contains(textToMatch.Text, StringComparison.OrdinalIgnoreCase))
                            {
                                returnStr = returnStr.Replace(
                                    textToMatch.Text,
                                    string.Empty,
                                    StringComparison.OrdinalIgnoreCase);
                            }

                            removesUsedList.Add(textToMatch.Text);
                        }
                        
                        continue;
                    }

                    if (returnStr.Contains(textToMatch.Text, StringComparison.OrdinalIgnoreCase))
                    {
                        returnStr = ReplaceFirst(returnStr, textToMatch.Text, string.Empty);
                    }

                    removesUsedList.Add(textToMatch.Text);
                    continue;
                }

                if (textToMatch.RemoveWholeLine)
                {
                    removesUsedList.Add(returnStr);
                    returnStr = string.Empty;

                    continue;
                }

                if (returnStr.Contains(textToMatch.Text, StringComparison.OrdinalIgnoreCase))
                {
                    var anyFound = false;
                    var loopIdx = 0;
                    
                    while (loopIdx++ <= 10)
                    {
                        var indexOf = returnStr.IndexOf(
                            textToMatch.Text,
                            StringComparison.OrdinalIgnoreCase);

                        if (indexOf == -1)
                        {
                            break;
                        }

                        var isCharBefore = indexOf >= 1 && !char.IsWhiteSpace(returnStr[indexOf - 1]);
                        var isCharAfter = returnStr.Length > indexOf + textToMatch.Text.Length
                            && !char.IsWhiteSpace(returnStr[indexOf + textToMatch.Text.Length])
                            && returnStr[indexOf + textToMatch.Text.Length] != '.';
                        
                        if (textToMatch.ExceptWhenInsideWord && (isCharBefore || isCharAfter))
                        {
                            break;
                        }

                        returnStr = ReplaceFirst(returnStr, textToMatch.Text, string.Empty);
                        anyFound = true;
                    }

                    if (!anyFound)
                    {
                        continue;
                    }
                }

                removesUsedList.Add(textToMatch.Text);
            }
        }

        removesUsed = removesUsedList.Count != 0 ? removesUsedList : null;
        return FormattingHelper.TrimFormatting(returnStr, trimPunctuationStart, trimPunctuationEnd)!;
    }
    
    private static string ReplaceFirst(string text, string search, string replace)
    {
        var pos = text.IndexOf(search, StringComparison.OrdinalIgnoreCase);
        
        if (pos < 0)
        {
            return text;
        }
        
        return text[..pos] + replace + text[(pos + search.Length)..];
    }
    
    [GeneratedRegex(@"[a-zA-Z]\d[a-zA-Z]")]
    private static partial Regex CharDigitCharRegex();

    public static bool IsCorruptedWord(DocumentLineWord? word, bool isOcr)
    {
        if (word == null || !isOcr)
        {
            return false;
        }
        
        var digitsCount = word.Text.Count(char.IsDigit);
        var totalConfidence = 0.0;
        const int minConfidence = 38;
        
        if (word.OcrConfidence == null)
        {
            return false;
        }
        
        var wordWithoutPunctuation = new string(word.Text
            .Where(ch =>
                ch != '"'
                && ch != '\''
                && ch != '('
                && ch != ')'
                && ch != ','
                && ch != '.'                
                && ch != ':')
            .ToArray());
        
        var confidence = word.OcrConfidence.Value;
        
        if (confidence < minConfidence
            && word.Text.Length >= 5
            && (AutoCorrectHelper.CustomDictionary.Check(wordWithoutPunctuation)
                || AutoCorrectHelper.Dictionary.Check(wordWithoutPunctuation)))
        {
            confidence = 100.0;
        }

        if (word.Autocorrected)
        {
            confidence = 100.0;
        }
            
        totalConfidence += confidence * word.Text.Length;
        var firstWord = word;
        
        if (char.IsDigit(firstWord.Text[0])
            && (firstWord.Text.EndsWith("st", StringComparison.OrdinalIgnoreCase)
                || firstWord.Text.EndsWith("nd", StringComparison.OrdinalIgnoreCase)
                || firstWord.Text.EndsWith("rd", StringComparison.OrdinalIgnoreCase)            
                || firstWord.Text.EndsWith("th", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }
        
        // TODO - why? shouldnt need to
        if (word.Text.Equals("per", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        
        var lineLength = word.Text.Length;
        var averageConfidence = totalConfidence / lineLength;
        var averageConfidenceBelowThreshold = averageConfidence is > 0 and < minConfidence;
        
        var lineLengthWithoutDots = word.Text.Count(c => c != '.');
        var mainlyDigits = ((100.0 / lineLengthWithoutDots) * digitsCount) >= 57;
        
        if (averageConfidenceBelowThreshold && !mainlyDigits)
        {
            return true;
        }

        const int checkIfUnderConfidence = 69;

        var wordWithoutPunctuationAndDigits = new string(wordWithoutPunctuation
            .Where(ch => !char.IsDigit(ch))
            .ToArray());
        
        if (!mainlyDigits
            && !wordWithoutPunctuation.All(char.IsUpper)
            && averageConfidence < checkIfUnderConfidence
            && !AutoCorrectHelper.CustomDictionary.Check(wordWithoutPunctuationAndDigits)
            && !AutoCorrectHelper.Dictionary.Check(wordWithoutPunctuationAndDigits))
        {
            return true;
        }
        
        var isCorrupt = IsCorruptedWordText(
            word.Text,
            isOcr,
            out _);
        
        return isCorrupt;
    }
    
    public static bool IsCorruptedLine(List<DocumentLineWord?>? words, bool isOcr, double unacceptableIncorrectValue = 50.01)
    {
        if (!isOcr || words == null)
        {
            return false;
        }
        
        var digitsCount = 0;
        var totalConfidence = 0.0;
        const int minConfidence = 38;
        
        foreach (var word in words)
        {
            digitsCount += word?.Text.Count(char.IsDigit) ?? 0;
            
            if (word?.OcrConfidence == null)
            {
                continue;
            }
            
            var confidence = word.OcrConfidence.Value;
            
            if (confidence < minConfidence
                && word.Text.Length >= 5
                && (AutoCorrectHelper.CustomDictionary.Check(word.Text) || AutoCorrectHelper.Dictionary.Check(word.Text)))
            {
                confidence = 100.0;
            }

            if (word.Autocorrected)
            {
                confidence = 100.0;
            }
                
            totalConfidence += confidence * word.Text.Length;
        }

        var firstWord = words[0]!;
        if (char.IsDigit(firstWord.Text[0])
            && (firstWord.Text.EndsWith("st", StringComparison.OrdinalIgnoreCase)
                || firstWord.Text.EndsWith("nd", StringComparison.OrdinalIgnoreCase)
                || firstWord.Text.EndsWith("rd", StringComparison.OrdinalIgnoreCase)            
                || firstWord.Text.EndsWith("th", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }
        
        var lineLength = words.Sum(w => w?.Text.Length);
        var averageConfidence = totalConfidence / lineLength;
        var averageConfidenceBelowThreshold = averageConfidence is > 0 and < minConfidence;
        
        var lineLengthWithoutDots = words.Sum(w => w?.Text.Count(c => c != '.'));
        var mainlyDigits = ((100.0 / lineLengthWithoutDots) * digitsCount) >= 60;
        
        if (averageConfidenceBelowThreshold && !mainlyDigits)
        {
            return true;
        }
        
        var isCorrupt = IsCorruptedLine(
            string.Join(' ', words.Select(w => w?.Text)),
            isOcr,
            unacceptableIncorrectValue);
        
        return isCorrupt;
    }

    private static bool IsSpecialCharacter(char ch)
    {
        return 
            (!char.IsLetterOrDigit(ch) || !char.IsAscii(ch))
            && ch != ' '
            && ch != '|'
            && ch != '/'
            && ch != '.'
            && ch != '%'
            && ch != '('
            && ch != ')'
            && ch != ','
            && ch != '"'
            && ch != '‘'
            && ch != '\''
            && ch != '-'
            && ch != ':'
            && ch != ';'
            && ch != '£'
            && ch != '&'
            && ch != '*';
    }

    public static bool IsCorruptedWords(
        List<DocumentLineWord> words,
        bool isOcr,
        double unacceptableIncorrectValue = 50.01)
    {
        var tweakedWords = new List<DocumentLineWord>();
        
        var hasCorruptedWord = words.Any(word =>
        {
            var isCorrupt = IsCorruptedWordText(word.Text, isOcr, out var wordText);

            var newWord = word.Clone();
            newWord.Text = wordText!;
            tweakedWords.Add(newWord);
            
            return isCorrupt;
        });

        if (hasCorruptedWord)
        {
            return true;
        }
        
        var lineText = string.Join(' ', tweakedWords.Select(w => w.Text));
        return IsCorruptedLine(lineText, isOcr, unacceptableIncorrectValue);
    }

    public static bool IsCorruptedWordText(string? wordText, bool isOcr, out string? wordTextTweaked)
    {
        if (!isOcr || string.IsNullOrEmpty(wordText))
        {
            wordTextTweaked = wordText;
            return false;
        }
        
        if (wordText.Contains('—'))
        {
            wordText = wordText.Replace("—", "-");
        }
        
        if (wordText.Contains('”'))
        {
            wordText = wordText.Replace("”", "\"");
        }
        
        if (wordText.Contains('’'))
        {
            wordText = wordText.Replace("’", "'");
        }
        
        // Swap out to a shared method to do this, as its done in 3 places
        if (wordText.Contains('“'))
        {
            wordText = wordText.Replace("“", "\"");
        }

        wordTextTweaked = wordText;
        
        if (IsSpecialCharacter(wordText[0]))
        {
            return true;
        }

        return false;
    }

    public static bool IsCorruptedLine(string? lineText, bool isOcr, double unacceptableIncorrectValue = 50.01)
    {
        if (!isOcr || string.IsNullOrEmpty(lineText))
        {
            return false;
        }
        
        if (lineText.Contains('—'))
        {
            lineText = lineText.Replace("—", "-");
        }
        
        if (lineText.Contains('”'))
        {
            lineText = lineText.Replace("”", "\"");
        }
        
        if (lineText.Contains('’'))
        {
            lineText = lineText.Replace("’", "'");
        }
        
        if (lineText.Contains('“'))
        {
            lineText = lineText.Replace("“", "\"");
        }
        
        if (IsSpecialCharacter(lineText[0]))
        {
            return true;
        }

        var specialCharCount = lineText.Count(IsSpecialCharacter);

        if (lineText.Length < 8 && CharDigitCharRegex().IsMatch(lineText))
        {
            return true;
        }

        if (specialCharCount >= 3)
        {
            return true;
        }
        
        if ((char.IsLower(lineText[0]) || char.IsDigit(lineText[0])) && specialCharCount >= 1)
        {
            return true;
        }
        
        if (CompanyNameHelper.StartsWithCompanyOrPersonalPrefix(lineText))
        {
            return false;
        }

        if (CompanyNameHelper.EndsWithCompanyOrPersonalSuffix(lineText))
        {
            return false;
        }

        var newLine = new StringBuilder();
        
        var charIndex = 0;
        var anySpacesInserted = false;
        
        foreach (var c in lineText)
        {
            if (
                char.IsAsciiLetter(c)
                && charIndex > 0
                && char.IsDigit(lineText[charIndex - 1]))
            {
                newLine.Append(' ');
                anySpacesInserted = true;
            }

            newLine.Append(c);
            charIndex++;
        }

        if (anySpacesInserted)
        {
           lineText = newLine.ToString();
        }
        
        var wordsSplit = GetNoneDigitOrCertain2LetterWords(lineText.Split(' '));
        var percentagePerWord = 100.0 / wordsSplit.Count;
        
        var countOfVeryShortWordsOrSymbols = wordsSplit.Count(word => word.Length <= 2);
        var percentageOfShortWords = countOfVeryShortWordsOrSymbols * percentagePerWord;
        
        const double unacceptableShortWordsValue = 30.0;
        var manyAndMajorityVeryShortWords = countOfVeryShortWordsOrSymbols > 3
                && percentageOfShortWords >= unacceptableShortWordsValue;

        if (manyAndMajorityVeryShortWords)
        {
            return true;
        }
        
        var suspectedIncorrectWords = wordsSplit.Where(word =>
        {
            if (word.Equals("th", StringComparison.OrdinalIgnoreCase)
                || word.Equals("rd", StringComparison.OrdinalIgnoreCase)
                || word.Equals("nd", StringComparison.OrdinalIgnoreCase)
                || word.Equals("st", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            
            var wordWithoutPunctuation = new string(word
                .Where(ch =>
                    ch != '"'
                    && ch != '\''
                    && ch != '('
                    && ch != ')'
                    && ch != ','
                    && ch != ':')
                .ToArray());

            return !word.Contains('/')
                && !double.TryParse(PotentialNumber(word), out _)
                && !AutoCorrectHelper.CustomDictionary.Check(wordWithoutPunctuation)
                && !AutoCorrectHelper.Dictionary.Check(wordWithoutPunctuation);
        }).ToList();

        var countOfSuspectedIncorrectWords = suspectedIncorrectWords.Count;
        var percentageOfSuspectedIncorrectWords = countOfSuspectedIncorrectWords * percentagePerWord;
        
        var mostWordsIncorrectlySpelt = wordsSplit.Count >= 2
            && percentageOfSuspectedIncorrectWords >= unacceptableIncorrectValue;
        
        return mostWordsIncorrectlySpelt;
    }

    private static string PotentialNumber(string word)
    {
        if (word.Contains("TL"))
        {
            word = word.Replace("TL", string.Empty);
        }
        
        if (word.Contains(','))
        {
            word = word.Replace(",", string.Empty);
        }

        return word;
    }

    private static List<string> GetNoneDigitOrCertain2LetterWords(IEnumerable<string> words)
    {
        return words
            .Where(word =>
            {
                var wordLower = word.ToLower();
                
                return !word.Any(char.IsDigit)
                       && wordLower != "a"
                       && wordLower != "a,"
                       && wordLower != "b,"
                       && wordLower != "c,"
                       && wordLower != "d,"
                       && wordLower != "e,"
                       && wordLower != "as"
                       && wordLower != "at"
                       && wordLower != "be"
                       && wordLower != "is"
                       && wordLower != "of"
                       && wordLower != "on"
                       && wordLower != "or"
                       && wordLower != "to"
                       && !wordLower.StartsWith("ta", StringComparison.OrdinalIgnoreCase); // TA is an OS reference
            })
            .ToList();
    }
    
    public static void NullOutSubLabels(IReadOnlyList<LabelGroupResult> matches)
    {
        foreach (var match in matches)
        {
            if (match.MatchedLabel != null)
            {
                match.MatchedLabel.SubLabels = null;
            }

            NullOutSubLabels(match.SubResults);
        }
    }

    public static string? GetTextFromFirstMatchByLabelGroup(
        IEnumerable<LabelGroupResult> matches,
        string labelGroupName,
        out LabelGroupResult? matchedLabelGroup)
    {
        var labelMatch = GetFirstMatchByLabelGroup(matches, labelGroupName);
        matchedLabelGroup = labelMatch;
        
        return GetFirstLineTextFromMatch(labelMatch);
    }
    
    public static string? GetTextFromFirstMatchByLabel(IEnumerable<LabelGroupResult> matches, string name)
    {
        return GetFirstLineTextFromMatch(GetFirstMatchByLabel(matches, name));
    }
    
    public static LabelGroupResult? GetFirstMatchByLabelGroup(IEnumerable<LabelGroupResult> matches, string labelGroupName)
    {
        return matches.FirstOrDefault(result => result.LabelGroupName == labelGroupName);
    }
    
    public static LabelGroupResult? GetFirstMatchByLabel(IEnumerable<LabelGroupResult> matches, string name)
    {
        return matches.FirstOrDefault(result => result.MatchedLabel?.Name == name);
    }
    
    public static IEnumerable<LabelGroupResult> GetMatchesByLabelGroup(IEnumerable<LabelGroupResult> matches, string labelGroupName)
    {
        return matches.Where(result => result.LabelGroupName == labelGroupName);
    }
    
    public static IEnumerable<LabelGroupResult> GetMatchesByLabel(IEnumerable<LabelGroupResult> matches, string name)
    {
        return matches.Where(result => result.MatchedLabel?.Name == name);
    }
    
    public static string? GetFirstLineTextFromMatch(LabelGroupResult? match)
    {
        return match?
            .Text?
            .FirstOrDefault()?
            .Text;
    }

    public static bool LikelyMapPage(IReadOnlyList<DocumentLine> documentLines, int numberOfImages)
    {
        if (documentLines.Count == 0)
        {
            return false;
        }

        if (numberOfImages > 10)
        {
            return true;
        }
        
        var containsAPhraseSuggestingItsAMap = documentLines
            .Any(l => l.Text.Contains("Map accompanying ", StringComparison.OrdinalIgnoreCase)
              || l.Text.Contains("Location Map ", StringComparison.OrdinalIgnoreCase)
              || l.Text.Contains("REFERENCE DRAWINGS", StringComparison.OrdinalIgnoreCase));

        if (containsAPhraseSuggestingItsAMap)
        {
            return true;
        }
                    
        var averageLineLength = documentLines.Average(line => line.Text.Length);
        const int minAverageLineLength = 15;
        
        // Short lines indicate it may be a map page,
        // no point processing that with the other services
        if (averageLineLength < minAverageLineLength)
        {
            return true;
        }

        return false;
    }
}