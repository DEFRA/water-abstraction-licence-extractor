using System.Text.RegularExpressions;
using WALE.ProcessFile.Services.Formats;
using WALE.ProcessFile.Services.Models;
using WeCantSpell.Hunspell;

namespace WALE.ProcessFile.Services.Helpers;

public static partial class DataHelper
{
    public static readonly WordList Dictionary = WordList.CreateFromFiles("en_GB.dic");

    public static List<DocumentLine> RemoveExcludesAndNotContains(
        LabelToMatch label,
        IReadOnlyList<DocumentLine>? betweenText,
        bool removeNotContains,
        out IReadOnlyList<string>? removesUsed)
    {
        removesUsed = null;
        var inputList = betweenText ?? new List<DocumentLine>();
        
        if ((label.Remove?.Any() != true && label.ResultMustNotContain?.Any() != true) || betweenText == null)
        {
            return FormattingHelper.RemoveMultipleBlankLines(inputList);
        }

        var returnList = new List<DocumentLine>();
        var removesUsedList = new List<string>();
        
        foreach (var line in inputList)
        {
            _ = RemoveExcludes(label, line.Text, true, out var removesUsedLoopOuter);

            // The whole line wants removing
            if (removesUsedLoopOuter?.Contains(line.Text) == true)
            {
                continue;
            }

            var newColumns = new List<DocumentLineColumn>();
            
            foreach (var column in line.Columns)
            {
                if (removeNotContains && LabelMatchingHelper.TextContainsForbiddenResult(column.Text, label))
                {
                    continue;
                }

                var isLastColumn = line.Columns.Last() == column;
                var alteredText = RemoveExcludes(label, column.Text, isLastColumn, out var removesUsedLoop);
                var clonedColumn = new DocumentLineColumn(alteredText);
                newColumns.Add(clonedColumn);

                if (removesUsedLoop != null)
                {
                    removesUsedList.AddRange(removesUsedLoop);
                }
            }
            
            var clonedLine = line.Clone(newColumns);
            returnList.Add(clonedLine);
        }

        removesUsed = removesUsedList;
        return FormattingHelper.RemoveMultipleBlankLines(returnList);
    }
    
    public static string RemoveExcludes(
        LabelToMatch label,
        string betweenText,
        bool trimPunctuation,
        out IReadOnlyList<string>? removesUsed)
    {
        removesUsed = null;
        
        if ((label.Remove?.Any() != true && label.ResultMustNotContain?.Any() != true) || string.IsNullOrEmpty(betweenText))
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

                if (textToMatch.ColumnMustStartWith)
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
                            returnStr = returnStr.Replace(
                                textToMatch.Text,
                                string.Empty,
                                StringComparison.InvariantCultureIgnoreCase);

                            removesUsedList.Add(textToMatch.Text);
                        }
                        
                        continue;
                    }
                    
                    returnStr = returnStr.Replace(
                        textToMatch.Text,
                        string.Empty,
                        StringComparison.InvariantCultureIgnoreCase);

                    removesUsedList.Add(textToMatch.Text);
                    continue;
                }

                if (textToMatch.RemoveWholeLine)
                {
                    removesUsedList.Add(returnStr);
                    returnStr = string.Empty;

                    continue;
                }

                returnStr = returnStr.Replace(
                    textToMatch.Text,
                    string.Empty,
                    StringComparison.InvariantCultureIgnoreCase);

                removesUsedList.Add(textToMatch.Text);
            }
        }

        removesUsed = removesUsedList.Count != 0 ? removesUsedList : null;
        return FormattingHelper.TrimFormatting(returnStr, trimPunctuation)!;
    }
    
    [GeneratedRegex(@"[a-zA-Z]\d[a-zA-Z]")]
    private static partial Regex CharDigitCharRegex();
    
    public static bool IsCorruptedText(string? line, double unacceptableIncorrectValue = 50.01)
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
            .Replace("'", string.Empty)
            .Replace("-", string.Empty)
            .Replace(":", string.Empty)
            .Replace(";", string.Empty)
            .Replace("£", string.Empty)
            .Replace("*", string.Empty)            
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
        
        if (CompanyName.StartsWithCompanyOrPersonalPrefix(line))
        {
            return false;
        }

        if (CompanyName.EndsWithCompanyOrPersonalSuffix(line))
        {
            return false;
        }
        
        var wordsSplit = GetNoneDigitWords(line.Split(' '));
        var percentagePerWord = 100.0 / wordsSplit.Count;
        
        var countOfVeryShortWordsOrSymbols = wordsSplit.Count(word => word.Length <= 2);
        var percentageOfShortWords = countOfVeryShortWordsOrSymbols * percentagePerWord;
        
        const double unacceptableShortWordsValue = 25.0;
        var manyAndMajorityVeryShortWords = countOfVeryShortWordsOrSymbols > 3
                && percentageOfShortWords >= unacceptableShortWordsValue;

        if (manyAndMajorityVeryShortWords)
        {
            return true;
        }
        
        var countOfSuspectedIncorrectWords = wordsSplit.Count(word =>
        {
            var wordWithoutPunctuation = word
                .Replace("\"", string.Empty)
                .Replace("'", string.Empty)
                .Replace("(", string.Empty)
                .Replace(")", string.Empty)
                .Replace(",", string.Empty)
                .Replace(":", string.Empty);

            return !Dictionary.Check(wordWithoutPunctuation)
                   && !word.Contains('/')
                   && !double.TryParse(word.Replace("TL", string.Empty).Replace(",", string.Empty), out _);
        });
        
        var percentageOfSuspectedIncorrectWords = countOfSuspectedIncorrectWords * percentagePerWord;

        var mostWordsIncorrectlySpelt = wordsSplit.Count >= 2
            && percentageOfSuspectedIncorrectWords >= unacceptableIncorrectValue;
        
        return mostWordsIncorrectlySpelt;
    }

    private static List<string> GetNoneDigitWords(IEnumerable<string> words)
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
                       && !wordLower.StartsWith("ta"); // TA is an OS reference
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