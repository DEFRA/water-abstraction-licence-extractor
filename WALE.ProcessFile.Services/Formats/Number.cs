using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Services.Formats;

public static class Number
{
    public const string Constant = "Number";
    
    public static bool AnyIsNumber(
        IReadOnlyList<DocumentLine?> linesList,
        LabelToMatch? label,
        bool isOcr,
        out List<DocumentLine> matchedLines)
    {
        matchedLines = [];
        
        var matched = false;
        var returnLines = new List<(string Text, DocumentLine OriginalLine)>();

        var firstLine = linesList.FirstOrDefault();
        
        var lineNumber = firstLine?.LineNumber ?? PositionConstants.UnknownLineNumber;
        var pageNumber = firstLine?.PageNumber ?? PositionConstants.UnknownPageNumber;
        
        foreach (var line in linesList)
        {
            if (DataHelper.IsCorruptedLine(line?.Text, isOcr))
            {
                if (matched)
                {
                    break;
                }
                
                continue;
            }

            foreach (var word in line!.Text.Split(PositionConstants.SpaceChar))
            {
                var wordWithoutBrackets = word
                    .Replace("(", string.Empty)
                    .Replace(")", string.Empty)
                    .Replace("*", string.Empty);

                var wordWithoutTrailingPeriod = wordWithoutBrackets;
                if (wordWithoutTrailingPeriod.EndsWith('.'))
                {
                    wordWithoutTrailingPeriod = wordWithoutTrailingPeriod[..^1];
                }
                
                if (!double.TryParse(wordWithoutTrailingPeriod, out var numberLineDbl))
                {
                    continue;
                }

                if (word == $"({numberLineDbl})")
                {
                    returnLines.Add(($"({numberLineDbl})", line));
                }
                else
                {
                    returnLines.Add((numberLineDbl + string.Empty, line));   
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
        
        foreach (var returnLine in returnLines)
        {
            if (label != null && LabelMatchingHelper.ShouldSkipResultAsForbidden(returnLine.Text, label))
            {
                continue;
            }

            var coords = returnLine
                .OriginalLine
                .Columns
                .First()
                .Words
                .First()
                .Coordinates;
            
            var columns = new List<DocumentLineColumn>
            {
                new([new(returnLine.Text, null, coords, null)])
            };

            var documentLine = new DocumentLine(
                lineNumber,
                pageNumber,
                columns,
                returnLine.OriginalLine.Top,
                returnLine.OriginalLine.Right,
                returnLine.OriginalLine.Bottom,
                returnLine.OriginalLine.Left);

            matchedLines.Add(documentLine);
        }

        return matched;
    }
}