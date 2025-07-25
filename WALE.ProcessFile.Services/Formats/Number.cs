using System.Globalization;
using WALE.ProcessFile.Services.Helpers;
using WALE.ProcessFile.Services.Models;

namespace WALE.ProcessFile.Services.Formats;

public static class Number
{
    public static bool TryGetNumber(
        string? text,
        int lineNumber,
        int pageNumber,
        out List<DocumentLine> numbers)
    {
        numbers = [];
        
        if (text == null)
        {
            return false;
        }
        
        var irrelevantWords = new List<DocumentLineWord>();

        var list = text
            .Split(' ')
            .Select(result => new DocumentLine(
                result,
                lineNumber,
                pageNumber,
                irrelevantWords,
                -1,
                -1,
                -1,
                -1));
        
        if (AnyIsNumber(list, out var numberLines))
        {
            numbers.AddRange(numberLines);
            return true;
        }

        return false;
    }
    
    public static bool AnyIsNumber(
        IEnumerable<DocumentLine?> lines,
        out List<DocumentLine> numberLines)
    {
        numberLines = [];

        var matched = false;
        var returnLines = new List<double>();

        var ls = lines.ToList();
        
        var lineNumber = ls.FirstOrDefault()?.LineNumber ?? -1;
        var pageNumber = ls.FirstOrDefault()?.PageNumber ?? -1;
        var lineWords = new List<DocumentLineWord>();
        
        foreach (var line in ls)
        {
            if (DataHelper.IsCorruptedText(line?.Text))
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
        }

        if (returnLines.Count > 0)
        {
            foreach (var tempLine in returnLines.OrderByDescending(text => text))
            {
                numberLines.Add(new DocumentLine(
                    tempLine.ToString(CultureInfo.InvariantCulture),
                    lineNumber,
                    pageNumber,
                    lineWords,
                    -1,
                    -1,
                    -1,
                    -1));
            }
        }
        
        return matched;
    }
}