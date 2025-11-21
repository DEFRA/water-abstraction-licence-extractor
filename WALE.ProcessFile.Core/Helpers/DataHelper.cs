using System.Text;
using System.Text.RegularExpressions;
using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Models;
using WeCantSpell.Hunspell;

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
            _ = RemoveExcludes(label, line.Text, false, false, out var removesUsedLoopOuter);

            // The whole line wants removing
            if (removesUsedLoopOuter?.Contains(line.Text) == true)
            {
                continue;
            }

            var newColumns = new List<DocumentLineColumn>();
            
            foreach (var column in line.Columns)
            {
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
                    out var removesUsedLoop);
                
                var clonedColumn = new DocumentLineColumn(alteredText);
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
        List<TextAndLabel>? textBeforeAtAndAfterLabel,
        bool includeLabelText)
    {
        var beforeStuff = textBeforeAtAndAfterLabel!
            .Where(tuple =>
                (includeLabelText && tuple.Label?.Position == LabelPosition.ActuallyLabel)
                    || tuple.Label?.Position is LabelPosition.LabelIsBeforeTextToFind
                        or LabelPosition.TextToFindIsBetweenLabels)
            .OrderBy(x =>
            {
                return x.Label?.Position switch
                {
                    LabelPosition.LabelIsAfterTextToFind => -2,
                    LabelPosition.ActuallyLabel => -1,
                    LabelPosition.TextToFindIsBetweenLabels => 0,
                    LabelPosition.LabelIsBeforeTextToFind => 1,
                    _ => throw new ArgumentOutOfRangeException()
                };
            })
            .Select(x => x.Text)
            .ToArray();
        
        return string.Join(' ', beforeStuff);
    }
    
    public static string RemoveExcludes(
        LabelToMatch label,
        string betweenText,
        bool trimPunctuationStart,
        bool trimPunctuationEnd,
        out IReadOnlyList<string>? removesUsed)
    {
        removesUsed = null;
        
        if ((label.Remove?.Any() != true && label.IgnoreMatchIfContains?.Any() != true) || string.IsNullOrEmpty(betweenText))
        {
            return betweenText;
        }
        
        var returnStr = betweenText;
        var removesUsedList = new List<string>();

        if (label.Remove != null)
        {
            foreach (var textToMatch in label.Remove)
            {
                if (textToMatch.Text.StartsWith('/') && textToMatch.Text.EndsWith('/'))
                {
                    var pattern = textToMatch.Text.Substring(1, textToMatch.Text.Length - 2);

                    if (Regex.IsMatch(returnStr, pattern))
                    {
                        returnStr = Regex.Replace(
                            returnStr,
                            pattern,
                            string.Empty);

                        removesUsedList.Add(textToMatch.Text);
                    }

                    continue;
                }

                if (!returnStr.Contains(textToMatch.Text))
                {
                    continue;
                }

                if (textToMatch.ColumnMustStartWith || textToMatch.LineMustStartWith)
                {
                    if (!returnStr.StartsWith(textToMatch.Text))
                    {
                        continue;
                    }
                        
                    if (textToMatch.ColumnMustHave2SequentialNumbers)
                    {
                        var words = returnStr.Split(' ');
                        var countOfNumbers = words.Count(word => int.TryParse(word, out _));

                        if (countOfNumbers >= 2)
                        {
                            if (returnStr.Contains(textToMatch.Text, StringComparison.InvariantCultureIgnoreCase))
                            {
                                returnStr = returnStr.Replace(
                                    textToMatch.Text,
                                    string.Empty,
                                    StringComparison.InvariantCultureIgnoreCase);
                            }

                            removesUsedList.Add(textToMatch.Text);
                        }
                        
                        continue;
                    }

                    if (returnStr.Contains(textToMatch.Text, StringComparison.InvariantCultureIgnoreCase))
                    {
                        returnStr = returnStr.Replace(
                            textToMatch.Text,
                            string.Empty,
                            StringComparison.InvariantCultureIgnoreCase);
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

                if (returnStr.Contains(textToMatch.Text, StringComparison.InvariantCultureIgnoreCase))
                {
                    returnStr = returnStr.Replace(
                        textToMatch.Text,
                        string.Empty,
                        StringComparison.InvariantCultureIgnoreCase);
                }

                removesUsedList.Add(textToMatch.Text);
            }
        }

        removesUsed = removesUsedList.Count != 0 ? removesUsedList : null;
        return FormattingHelper.TrimFormatting(returnStr, trimPunctuationStart, trimPunctuationEnd)!;
    }
    
    [GeneratedRegex(@"[a-zA-Z]\d[a-zA-Z]")]
    private static partial Regex CharDigitCharRegex();

    public static bool IsCorruptedLine(List<DocumentLineWord?>? words, double unacceptableIncorrectValue = 50.01)
    {
        if (words == null)
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
                
            totalConfidence += confidence * word.Text.Length;
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
        
        var isCorrupt = IsCorruptedText(
            string.Join(' ', words.Select(w => w?.Text)),
            true,
            unacceptableIncorrectValue);
        
        return isCorrupt;
    }

    public static bool IsCorruptedText(string? line, bool isPartialChunk = false, double unacceptableIncorrectValue = 50.01)
    {
        if (string.IsNullOrEmpty(line))
        {
            return false;
        }
        
        if (line.Contains('—'))
        {
            line = line.Replace("—", "-");
        }
        
        if (line.Contains('”'))
        {
            line = line.Replace("”", "\"");
        }
        
        if (line.Contains('’'))
        {
            line = line.Replace("’", "'");
        }

        // TODO sort common misreadings
        if (line.Contains("dayof", StringComparison.InvariantCultureIgnoreCase))
        {
            line = line.Replace("dayof", "day of");
        }
        
        var containsSpecialChar = line
            .Where(ch =>
                ch != ' '
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
                && ch != '*')
            .Count(ch => !char.IsLetterOrDigit(ch) || !char.IsAscii(ch));

        if (line.Length < 8 && CharDigitCharRegex().IsMatch(line))
        {
            return true;
        }

        if (containsSpecialChar >= 3)
        {
            return true;
        }
        
        if ((char.IsLower(line[0]) || char.IsDigit(line[0])) && containsSpecialChar >= 1)
        {
            return true;
        }
        
        if (CompanyNameHelper.StartsWithCompanyOrPersonalPrefix(line))
        {
            return false;
        }

        if (CompanyNameHelper.EndsWithCompanyOrPersonalSuffix(line))
        {
            return false;
        }

        var newLine = new StringBuilder();
        
        var charIndex = 0;
        var anySpacesInserted = false;
        
        foreach (var c in line)
        {
            if (
                char.IsAsciiLetter(c)
                && charIndex > 0
                && char.IsDigit(line[charIndex - 1]))
            {
                newLine.Append(' ');
                anySpacesInserted = true;
            }

            newLine.Append(c);
            charIndex++;
        }

        if (anySpacesInserted)
        {
           line = newLine.ToString();
        }
        
        var wordsSplit = GetNoneDigitOrCertain2LetterWords(line.Split(' '));
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
            if (word.Equals("th", StringComparison.InvariantCultureIgnoreCase)
                || word.Equals("rd", StringComparison.InvariantCultureIgnoreCase)
                || word.Equals("nd", StringComparison.InvariantCultureIgnoreCase)
                || word.Equals("st", StringComparison.InvariantCultureIgnoreCase))
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
                       && !wordLower.StartsWith("ta", StringComparison.InvariantCultureIgnoreCase); // TA is an OS reference
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
}