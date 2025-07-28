using System.Globalization;
using WALE.ProcessFile.Services.Constants;
using WALE.ProcessFile.Services.Helpers;
using WALE.ProcessFile.Services.Models;

namespace WALE.ProcessFile.Services.Formats;

public static class Number
{
    public const string Constant = "Number";
    
    public static bool TryGetNumber(
        string? text,
        int lineNumber,
        int pageNumber,
        out List<DocumentLine> matchedNumbers)
    {
        matchedNumbers = [];
        
        if (text == null)
        {
            return false;
        }
        
        var emptyIrrelevantWords = new List<DocumentLineWord>();

        var list = text
            .Split(PositionConstants.SpaceChar)
            .Select(result => new DocumentLine(
                result,
                lineNumber,
                pageNumber,
                emptyIrrelevantWords,
                PositionConstants.UnknownCoOrdinate,
                PositionConstants.UnknownCoOrdinate,
                PositionConstants.UnknownCoOrdinate,
                PositionConstants.UnknownCoOrdinate));

        if (!AnyIsNumber(list, out var numberLines))
        {
            return false;
        }
        
        matchedNumbers.AddRange(numberLines);
        return true;

    }
    
    public static bool AnyIsNumber(
        IEnumerable<DocumentLine?> lines,
        out List<DocumentLine> matchedLines)
    {
        matchedLines = [];

        var matched = false;
        var returnLines = new List<double>();

        var linesList = lines.ToList();
        var firstLine = linesList.FirstOrDefault();
        
        var lineNumber = firstLine?.LineNumber ?? PositionConstants.UnknownLineNumber;
        var pageNumber = firstLine?.PageNumber ?? PositionConstants.UnknownPageNumber;
        var lineWords = new List<DocumentLineWord>();
        
        foreach (var line in linesList)
        {
            if (DataHelper.IsCorruptedText(line?.Text))
            {
                if (matched)
                {
                    break;
                }
                
                continue;
            }

            foreach (var word in line!.Text.Split(PositionConstants.SpaceChar))
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

        if (returnLines.Count == 0)
        {
            return matched;
        }
        
        foreach (var tempLine in returnLines.OrderByDescending(text => text))
        {
            matchedLines.Add(new DocumentLine(
                tempLine.ToString(CultureInfo.InvariantCulture),
                lineNumber,
                pageNumber,
                lineWords,
                 PositionConstants.UnknownCoOrdinate,
                 PositionConstants.UnknownCoOrdinate,
                 PositionConstants.UnknownCoOrdinate,
                 PositionConstants.UnknownCoOrdinate));
        }

        return matched;
    }
}