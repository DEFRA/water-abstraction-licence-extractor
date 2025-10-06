using WALE.ProcessFile.Services.Constants;
using WALE.ProcessFile.Services.Models;

namespace WALE.ProcessFile.Services.Helpers;

public static class OcrHelper
{
    public static IReadOnlyList<DocumentLine> Group(
        IReadOnlyList<LineAndWords> returnLines,
        int pageNumber,
        int lineHeight)
    {
        var lineNumber = 0;
        
        LineAndWords? previousLine = null;
        var lineIndex = 0;
        
        // BoundingBox is { X top left, Y top left , X top right , Y top right,
        // X bottom right , Y bottom right , X bottom left , Y bottom left }
        
        return returnLines
            .Where(line => !FormattingHelper.IsNullOrEmptyWhitespaceOrPunctuation(line.Text))
            .Where(line => !DataHelper.IsCorruptedText(line.Text, 100))
            .GroupBy(line =>
            {
                previousLine ??= line;

                var yDiff =
                    line.Words![0]!.Coordinates.Top
                    - (previousLine?.Words!)[0]!.Coordinates.Top;
                
                if (yDiff >= lineHeight)
                {
                    lineIndex += 1;
                }

                previousLine = line;
                return lineIndex;
            })
            .Select(lines =>
            {
                var columns = new List<DocumentLineColumn>();

                foreach (var line in lines.OrderBy(l => l.Words![0]!.Coordinates.Left))
                {
                    columns.Add(new DocumentLineColumn(line.Text!, line.Words!.Select(word =>
                        new DocumentLineWord(
                            word!.Text,
                            word.OcrConfidence * 100,
                            new DocumentLineWordCoordinates(
                                word.Coordinates.Top,
                                word.Coordinates.Right,
                                word.Coordinates.Bottom,
                                word.Coordinates.Left)))
                        .ToList())
                    );
                }
                
                var documentLine = new DocumentLine(
                    lineNumber++,
                    pageNumber,
                    columns,
                    PositionConstants.UnknownCoordinate,
                    PositionConstants.UnknownCoordinate,
                    PositionConstants.UnknownCoordinate);

                return documentLine;
            })
            .ToList();
    }
}