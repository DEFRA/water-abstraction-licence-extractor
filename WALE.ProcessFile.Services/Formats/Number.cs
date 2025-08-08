using System.Globalization;
using WALE.ProcessFile.Services.Constants;
using WALE.ProcessFile.Services.Helpers;
using WALE.ProcessFile.Services.Models;

namespace WALE.ProcessFile.Services.Formats;

public static class Number
{
    public const string Constant = "Number";
    
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

        var lineColumns = new List<DocumentLineColumn>();
        
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
                    continue;
                }

                returnLines.Add(numberLineDbl);

                if (!matched)
                {
                    lineNumber = line.LineNumber;
                    pageNumber = line.PageNumber;
                    lineColumns = line.Columns;
                }

                matched = true;
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
                lineColumns,
                 PositionConstants.UnknownCoordinate,
                 PositionConstants.UnknownCoordinate,
                 PositionConstants.UnknownCoordinate));
        }

        return matched;
    }
}