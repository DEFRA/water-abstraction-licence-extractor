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
        LabelToMatch? label,
        out List<DocumentLine> matchedLines)
    {
        matchedLines = [];

        if (lines?.FirstOrDefault()?.PageNumber == 4)
        {
            
        }
        
        var matched = false;
        var returnLines = new List<string>();

        var linesList = lines.ToList();
        var firstLine = linesList.FirstOrDefault();
        
        var lineNumber = firstLine?.LineNumber ?? PositionConstants.UnknownLineNumber;
        var pageNumber = firstLine?.PageNumber ?? PositionConstants.UnknownPageNumber;
        
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
                var wordWithoutBrackers = word
                    .Replace("(", string.Empty)
                    .Replace(")", string.Empty);
                
                if (!double.TryParse(wordWithoutBrackers, out var numberLineDbl))
                {
                    continue;
                }

                if (word == $"({numberLineDbl})")
                {
                    returnLines.Add($"({numberLineDbl})");
                }
                else
                {
                    returnLines.Add(numberLineDbl + string.Empty);   
                }

                if (returnLines.Last().Contains(","))
                {
                    
                }

                if (!matched)
                {
                    lineNumber = line.LineNumber;
                    pageNumber = line.PageNumber;
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
            if (label != null && LabelMatchingHelper.TextContainsForbiddenResult(tempLine, label))
            {
                continue;
            }
            
            var columns = new List<DocumentLineColumn>
            {
                new(tempLine,[])
            };

            var documentLine = new DocumentLine(
                lineNumber,
                pageNumber,
                columns,
                PositionConstants.UnknownCoordinate,
                PositionConstants.UnknownCoordinate,
                PositionConstants.UnknownCoordinate);

            matchedLines.Add(documentLine);
        }

        return matched;
    }
}