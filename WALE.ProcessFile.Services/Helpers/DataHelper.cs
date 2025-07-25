using System.Text.RegularExpressions;
using WALE.ProcessFile.Services.Formats;
using WALE.ProcessFile.Services.Models;
using WeCantSpell.Hunspell;

namespace WALE.ProcessFile.Services.Helpers;

public static partial class DataHelper
{
    public static readonly WordList Dictionary = WordList.CreateFromFiles("en_GB.dic");

    public static List<DocumentLine> RemoveExcludes(
        LabelToMatch label,
        IReadOnlyList<DocumentLine>? betweenText,
        out IReadOnlyList<string>? removesUsed)
    {
        removesUsed = null;
        var returnList = betweenText != null ? [..betweenText] : new List<DocumentLine>();
        
        if (label.Remove?.Any() != true || betweenText == null)
        {
            return FormattingHelper.RemoveMultipleBlankLines(returnList);
        }

        var removesUsedList = new List<string>();
        
        for (var idx = 0; idx < returnList.Count; idx++)
        {
            var line = returnList[idx];
            
            returnList[idx] = new DocumentLine(
                RemoveExcludes(label, betweenText[idx].Text, out var removesUsedLoop),
                line.LineNumber,
                line.PageNumber,
                line.Words.ToList(),
                line.Top,
                line.TopRounded,
                line.Left,
                line.LeftRounded);

            if (removesUsedLoop != null)
            {
                removesUsedList.AddRange(removesUsedLoop);
            }
        }

        removesUsed = removesUsedList;
        return FormattingHelper.RemoveMultipleBlankLines(returnList);
    }
    
    public static string RemoveExcludes(
        LabelToMatch label,
        string betweenText,
        out IReadOnlyList<string>? removesUsed)
    {
        removesUsed = null;
        
        if (label.Remove?.Any() != true || string.IsNullOrEmpty(betweenText))
        {
            return betweenText;
        }

        var returnStr = betweenText;
        var removesUsedList = new List<string>();
        
        foreach (var textToMatch in label.Remove)
        {
            if (textToMatch.Text.StartsWith('/') && textToMatch.Text.EndsWith('/'))
            {
                var pattern = textToMatch.Text.Substring(1, textToMatch.Text!.Length - 2);

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

            if (textToMatch.LineMustStartWith && !returnStr.StartsWith(textToMatch.Text))
            {
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

        removesUsed = removesUsedList.Count != 0 ? removesUsedList : null;
        return FormattingHelper.TrimFormatting(returnStr)!;
    }
    
    [GeneratedRegex(@"[a-zA-Z]\d[a-zA-Z]")]
    private static partial Regex CharDigitCharRegex();
    
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
        
        if (CompanyName.StartsWithCompanyOrPersonalPrefix(line))
        {
            return false;
        }

        if (CompanyName.EndsWithCompanyOrPersonalSuffix(line))
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
}