using WALE.ProcessFile.Models;
using WALE.ProcessFile.Models.Constants;
using WALE.ProcessFile.Services.Helpers;

namespace WALE.ProcessFile.Services.Formats;

public static class Number
{
    public const string Constant = "Number";
    
    public static bool AnyIsNumber(
        IReadOnlyList<DocumentLine?> linesList,
        LabelToMatch? label,
        out List<DocumentLine> matchedLines)
    {
        matchedLines = [];
        
        var matched = false;
        var returnLines = new List<string>();

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
        
        foreach (var tempLine in returnLines)
        {
            if (label != null && LabelMatchingHelper.ShouldSkipResultAsForbidden(tempLine, label))
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