using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OutputSchema;

namespace WALE.ProcessFile.Core.Helpers;

public static class OcrHelper
{
    public static IReadOnlyList<DocumentLine> Group(
        IReadOnlyList<LineAndWords> returnLines,
        int pageNumber,
        int lineHeight,
        int wordGap,
        int minHeight)
    {
        const int unacceptableIncorrectValue = 80;
        var lineNumber = 0;
        
        LineAndWords? previousLine = null;
        var lineIndex = 0;
        
        // BoundingBox is { 0 X top left, 1 Y top left , 2 X top right , 3 Y top right,
        // 4 X bottom right , 5 Y bottom right , 6 X bottom left , 7 Y bottom left }

        var uncorruptedLines = returnLines
            .Where(line => !FormattingHelper.IsNullOrEmptyWhitespaceOrPunctuation(line.Text))
            .Where(line => !DataHelper.IsCorruptedLine(line.Words, unacceptableIncorrectValue))
            .ToList();
        
        var groupedLines = uncorruptedLines
            .GroupBy(line =>
            {
                previousLine ??= line;

                var yDiff = GetMidpoint(line.Words![0]!.Coordinates)
                    - GetMidpoint(previousLine?.Words![0]!.Coordinates);
                
                if (yDiff > lineHeight)
                {
                    lineIndex += 1;
                }

                previousLine = line;
                return lineIndex;
            })
            .Select(lines =>
            {
                var words = new List<DocumentLineWord>();
                
                foreach (var line in lines.OrderBy(l => l.Words![0]!.Coordinates.Left))
                {
                    if (line.Words == null)
                    {
                        continue;
                    }

                    var lineWords = new List<DocumentLineWord>();

                    foreach (var word in line.Words)
                    {
                        var wordHeight = word!.Coordinates.Bottom - word.Coordinates.Top;
                        
                        if (minHeight > wordHeight)
                        {
                            continue;
                        }

                        var wordTextWithoutPunctuation = word.Text
                            .Replace(",", string.Empty)
                            .Replace(".", string.Empty);
                        
                        if (word is { OcrConfidence: < 40, Text.Length: > 3 }
                            && wordTextWithoutPunctuation.Count(char.IsAsciiLetter) > 3
                            && !AutoCorrectHelper.CustomDictionary.Check(wordTextWithoutPunctuation)
                            && !AutoCorrectHelper.Dictionary.Check(wordTextWithoutPunctuation))
                        {
                            var topSuggestion = AutoCorrectHelper.GetTopSuggestion(wordTextWithoutPunctuation);

                            if (topSuggestion != null)
                            {
                                var lengthDiff = Math.Abs(topSuggestion.Length - word.Text.Length);

                                if (lengthDiff < 2)
                                {
                                    lineWords.Add(new DocumentLineWord(topSuggestion, word.OcrConfidence,
                                        word.Coordinates));
                                    
                                    continue;
                                }
                            }
                        }
                        
                        lineWords.Add(word);
                    }
                    
                    words.AddRange(lineWords);
                }

                var columns = new List<DocumentLineColumn>
                {
                    new()
                };
                
                DocumentLineWord? previousWord = null;
                
                foreach (var word in words.OrderBy(w => w.Coordinates.Left))
                {
                    var xDiff = word.Coordinates.Left - previousWord?.Coordinates.Right;

                    if (xDiff > wordGap)
                    {
                        columns.Add(new DocumentLineColumn());
                    }

                    var column = columns.Last();
                    column.Words.Add(word);

                    previousWord = word;
                }

                foreach (var column in columns)
                {
                    column.Text = string.Join(' ', column.Words.Select(w => w.Text));
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
            .Where(line => !DataHelper.IsCorruptedText(line.Text, false, unacceptableIncorrectValue))
            .ToList();

        return groupedLines;
    }
    
    private static double? GetMidpoint(DocumentLineWordCoordinates? coordinates)
    {
        if (coordinates == null)
        {
            return null;
        }
        
        return coordinates.Top + ((coordinates.Bottom - coordinates.Top) / 2);
    }
}