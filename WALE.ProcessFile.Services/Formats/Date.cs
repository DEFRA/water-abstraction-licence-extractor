using System.Text.RegularExpressions;
using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Services.Formats;

public static partial class Date
{
    public const string Constant = "Date";
    
    public static bool AnyIsDate(
        IEnumerable<DocumentLine?> lines,
        out List<DocumentLine> matchedLines)
    {
        matchedLines = lines.Where(line => IsDate(line?.Text)).ToList()!;
        return matchedLines.Count > 0;
    }

    public static bool ContainsDate(string? text, out List<string> dates)
    {
        dates = [];
        
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }
        
        var words = text.Split(' ');

        foreach (var word in words)
        {
            if (DateTime.TryParse(word, out _))
            {
                dates.Add(word);
            }
        }

        return dates.Count > 0;
    }
    
    public static bool IsDate(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        var tweakedText = text.Replace("o", "0", StringComparison.InvariantCultureIgnoreCase);
        
        return YearRegex().IsMatch(tweakedText)
            || ContainsMonth(tweakedText)
            || DateTime.TryParse(tweakedText, out _);
    }

    public static DateTime? GetDateFromString(string? input)
    {
        var dateString = DateFormatConsistent(input);
        
        return DateTime.TryParse(dateString, out var dateOfIssueOut)
            ? dateOfIssueOut
            : null;
    }

    public static string? DateFormatConsistent(string? input)
    {
        if (input == null)
        {
            return null;
        }
        
        if (ContainsMonthWord(input, out var monthWord, out var monthPosition))
        {
            var datePart = input[..monthPosition];
            var yearPart = input[(monthPosition + monthWord!.Length)..];

            var dateOnlyDigits = string.Join(string.Empty, datePart.Where(char.IsDigit).ToArray());
            if (string.IsNullOrEmpty(dateOnlyDigits))
            {
                // TODO This assumption should be logged - really we shouldn't do it at all
                dateOnlyDigits = "1";
            }
            else if (dateOnlyDigits.Length > 2)
            {
                dateOnlyDigits = dateOnlyDigits[..2];
            }

            if (dateOnlyDigits.StartsWith("4"))
            {
                dateOnlyDigits = $"1{dateOnlyDigits[1..]}";
            }
            
            var yearStr = string.Join(string.Empty, yearPart.Select(c => c == 'g' ? '6' : c).Where(char.IsDigit).ToArray());

            if (yearStr.Length == 4 && yearStr[..2] == "15")
            {
                yearStr = $"19{yearStr[2..]}";
            }
            else if (yearStr.Length > 4)
            {
                yearStr = yearStr[..4];
            }
            
            return $"{dateOnlyDigits}/{monthWord}/{yearStr}";
        }
        
        var countOfColons = input.Count(c => c == ':');
        if (countOfColons >= 2)
        {
            ReplaceIfContains(input, " ", string.Empty, out input);
            ReplaceIfContains(input, ".", string.Empty, out input);
            ReplaceIfContains(input, "/", string.Empty, out input);
        }
        
        var countOfSpaces = input.Count(c => c == ' ');
        if (countOfSpaces >= 2)
        {
            ReplaceIfContains(input, ":", string.Empty, out input);
            ReplaceIfContains(input, ".", string.Empty, out input);
            ReplaceIfContains(input, "/", string.Empty, out input);
        }
        
        var countOfSlashes = input.Count(c => c == '/');
        if (countOfSlashes >= 2)
        {
            ReplaceIfContains(input, " ", string.Empty, out input);
            ReplaceIfContains(input, ".", string.Empty, out input);
            ReplaceIfContains(input, ":", string.Empty, out input);
        }
        
        var countOfDots = input.Count(c => c == '.');
        if (countOfDots >= 2)
        {
            ReplaceIfContains(input, " ", string.Empty, out input);
            ReplaceIfContains(input, "/", string.Empty, out input);
            ReplaceIfContains(input, ":", string.Empty, out input);
        }
        
        ReplaceIfContains(input, "Signed", string.Empty, out input);
        ReplaceIfContains(input, "first", "1", out input);
        ReplaceIfContains(input, "second", "2", out input);
        ReplaceIfContains(input, "third", "3", out input);
        ReplaceIfContains(input, "fourth", "4", out input);
        ReplaceIfContains(input, "fifth", "5", out input);
        ReplaceIfContains(input, "sixth", "6", out input);
        ReplaceIfContains(input, "seventh", "7", out input);
        ReplaceIfContains(input, "eighth", "8", out input);
        ReplaceIfContains(input, "ninth", "9", out input);
        ReplaceIfContains(input, "tenth", "10", out input);
        ReplaceIfContains(input, "eleventh", "11", out input);
        ReplaceIfContains(input, "twelfth", "12", out input);
        ReplaceIfContains(input, "thirteenth", "13", out input);
        ReplaceIfContains(input, "fourteenth", "14", out input);
        ReplaceIfContains(input, "fifteenth", "15", out input);
        ReplaceIfContains(input, "sixteenth", "16", out input);
        ReplaceIfContains(input, "seventeenth", "17", out input);
        ReplaceIfContains(input, "eighteenth", "18", out input);
        ReplaceIfContains(input, "nineteenth", "19", out input);
        ReplaceIfContains(input, "twentieth", "20", out input);
        ReplaceIfContains(input, "twenty-first", "21", out input);
        ReplaceIfContains(input, "twenty-second", "22", out input);
        ReplaceIfContains(input, "twenty-third", "23", out input);
        ReplaceIfContains(input, "twenty-fourth", "24", out input);
        ReplaceIfContains(input, "twenty-fifth", "25", out input);
        ReplaceIfContains(input, "twenty-sixth", "26", out input);
        ReplaceIfContains(input, "twenty-seventh", "27", out input);
        ReplaceIfContains(input, "twenty-eighth", "28", out input);
        ReplaceIfContains(input, "twenty-ninth", "29", out input);
        ReplaceIfContains(input, "thirtieth", "30", out input);
        ReplaceIfContains(input, "thirty-first", "31", out input);
        ReplaceIfContains(input, "August", "Aug", out input);
        ReplaceIfContains(input, "DAYOF", string.Empty, out input);
        ReplaceIfContains(input, "st", string.Empty, out input);
        ReplaceIfContains(input, "nd", string.Empty, out input);
        ReplaceIfContains(input, "rd", string.Empty, out input);
        ReplaceIfContains(input, "IEH", string.Empty, out input); // misreading of TH
        ReplaceIfContains(input, "th", string.Empty, out input);

        if (input.Length >= 2 && !char.IsDigit(input[0]) && char.IsDigit(input[1]))
        {
            input = input[1..];
        }
        
        return input;
    }
    
    private static void ReplaceIfContains(string input, string match, string replaceWith, out string output)
    {
        output = input;

        if (!input.Contains(match, StringComparison.InvariantCultureIgnoreCase))
        {
            return;
        }
        
        output = input.Replace(match, replaceWith, StringComparison.InvariantCultureIgnoreCase);
    }
    
    private static bool ContainsMonthWord(string? input, out string? matchedWord, out int monthPosition)
    {
        matchedWord = null;
        monthPosition = -1;
        
        if (input == null)
        {
            return false;
        }
        
        ReplaceIfContains(input, "NAY", "MAY", out input); // misreading of TH - TODO should use autocorrect
        ReplaceIfContains(input, "NAY", "MAY", out input); // misreading of TH - TODO should use autocorrect
        ReplaceIfContains(input, "HAY", "MAY", out input); // misreading of TH - TODO should use autocorrect
        
        if (input.Contains("january", StringComparison.InvariantCultureIgnoreCase))
        {
            matchedWord = "january";
            monthPosition = input.IndexOf(matchedWord, StringComparison.InvariantCultureIgnoreCase);
            return true;
        }
        
        if (input.Contains("february", StringComparison.InvariantCultureIgnoreCase))
        {
            matchedWord = "february";
            monthPosition = input.IndexOf(matchedWord, StringComparison.InvariantCultureIgnoreCase);
            return true;
        }
        
        if (input.Contains("march", StringComparison.InvariantCultureIgnoreCase))
        {
            matchedWord = "march";
            monthPosition = input.IndexOf(matchedWord, StringComparison.InvariantCultureIgnoreCase);
            return true;
        }
        
        if (input.Contains("april", StringComparison.InvariantCultureIgnoreCase))
        {
            matchedWord = "april";
            monthPosition = input.IndexOf(matchedWord, StringComparison.InvariantCultureIgnoreCase);
            return true;
        }
        
        if (input.Contains("may", StringComparison.InvariantCultureIgnoreCase))
        {
            matchedWord = "may";
            monthPosition = input.IndexOf(matchedWord, StringComparison.InvariantCultureIgnoreCase);
            return true;
        }
        
        if (input.Contains("june", StringComparison.InvariantCultureIgnoreCase))
        {
            matchedWord = "june";
            monthPosition = input.IndexOf(matchedWord, StringComparison.InvariantCultureIgnoreCase);
            return true;
        }
        
        if (input.Contains("july", StringComparison.InvariantCultureIgnoreCase))
        {
            matchedWord = "july";
            monthPosition = input.IndexOf(matchedWord, StringComparison.InvariantCultureIgnoreCase);
            return true;
        }
        
        if (input.Contains("august", StringComparison.InvariantCultureIgnoreCase))
        {
            matchedWord = "august";
            monthPosition = input.IndexOf(matchedWord, StringComparison.InvariantCultureIgnoreCase);
            return true;
        }
        
        if (input.Contains("september", StringComparison.InvariantCultureIgnoreCase))
        {
            matchedWord = "september";
            monthPosition = input.IndexOf(matchedWord, StringComparison.InvariantCultureIgnoreCase);
            return true;
        }
        
        if (input.Contains("october", StringComparison.InvariantCultureIgnoreCase))
        {
            matchedWord = "october";
            monthPosition = input.IndexOf(matchedWord, StringComparison.InvariantCultureIgnoreCase);
            return true;
        }
        
        if (input.Contains("november", StringComparison.InvariantCultureIgnoreCase))
        {
            matchedWord = "november";
            monthPosition = input.IndexOf(matchedWord, StringComparison.InvariantCultureIgnoreCase);
            return true;
        }
        
        if (input.Contains("december", StringComparison.InvariantCultureIgnoreCase))
        {
            matchedWord = "december";
            monthPosition = input.IndexOf(matchedWord, StringComparison.InvariantCultureIgnoreCase);
            return true;
        }
        
        if (input.Contains("jan", StringComparison.InvariantCultureIgnoreCase))
        {
            matchedWord = "jan";
            monthPosition = input.IndexOf(matchedWord, StringComparison.InvariantCultureIgnoreCase);
            return true;
        }
        
        if (input.Contains("feb", StringComparison.InvariantCultureIgnoreCase))
        {
            matchedWord = "feb";
            monthPosition = input.IndexOf(matchedWord, StringComparison.InvariantCultureIgnoreCase);
            return true;
        }
        
        if (input.Contains("mar", StringComparison.InvariantCultureIgnoreCase))
        {
            matchedWord = "mar";
            monthPosition = input.IndexOf(matchedWord, StringComparison.InvariantCultureIgnoreCase);
            return true;
        }
        
        if (input.Contains("apr", StringComparison.InvariantCultureIgnoreCase))
        {
            matchedWord = "apr";
            monthPosition = input.IndexOf(matchedWord, StringComparison.InvariantCultureIgnoreCase);
            return true;
        }
        
        if (input.Contains("may", StringComparison.InvariantCultureIgnoreCase))
        {
            matchedWord = "may";
            monthPosition = input.IndexOf(matchedWord, StringComparison.InvariantCultureIgnoreCase);
            return true;
        }
        
        if (input.Contains("jun", StringComparison.InvariantCultureIgnoreCase))
        {
            matchedWord = "jun";
            monthPosition = input.IndexOf(matchedWord, StringComparison.InvariantCultureIgnoreCase);
            return true;
        }
        
        if (input.Contains("jul", StringComparison.InvariantCultureIgnoreCase))
        {
            matchedWord = "jul";
            monthPosition = input.IndexOf(matchedWord, StringComparison.InvariantCultureIgnoreCase);
            return true;
        }
        
        if (input.Contains("aug", StringComparison.InvariantCultureIgnoreCase))
        {
            matchedWord = "aug";
            monthPosition = input.IndexOf(matchedWord, StringComparison.InvariantCultureIgnoreCase);
            return true;
        }
        
        if (input.Contains("sep", StringComparison.InvariantCultureIgnoreCase))
        {
            matchedWord = "sep";
            monthPosition = input.IndexOf(matchedWord, StringComparison.InvariantCultureIgnoreCase);
            return true;
        }
        
        if (input.Contains("oct", StringComparison.InvariantCultureIgnoreCase))
        {
            matchedWord = "oct";
            monthPosition = input.IndexOf(matchedWord, StringComparison.InvariantCultureIgnoreCase);
            return true;
        }
        
        if (input.Contains("nov", StringComparison.InvariantCultureIgnoreCase))
        {
            matchedWord = "nov";
            monthPosition = input.IndexOf(matchedWord, StringComparison.InvariantCultureIgnoreCase);
            return true;
        }
        
        if (input.Contains("dec", StringComparison.InvariantCultureIgnoreCase))
        {
            matchedWord = "dec";
            monthPosition = input.IndexOf(matchedWord, StringComparison.InvariantCultureIgnoreCase);
            return true;
        }

        return false;
    }
    
    private static bool ContainsMonth(string text)
    {
        var months = new List<string>
        {
            " Jan ",
            "January",
            " Feb ",
            "February",
            " Mar ",
            "March",
            " Apr ",
            "April",
            " May ",
            " Jun ",
            "June",
            " Jul ",
            "July",
            " Aug ",
            " August",
            " Sep ",
            " September",
            " Oct ",
            "October",
            " Nov ",
            "November",
            " Dec ",
            " December"
        };
        
        return months.Any(m => text.Contains(m, StringComparison.InvariantCultureIgnoreCase));
    }
    
    [GeneratedRegex(@"18 ?\d\d|19 ?\d\d|20 ?\d\d")]
    private static partial Regex YearRegex();
}