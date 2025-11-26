using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OutputSchema;

namespace WALE.ProcessFile.Core.Helpers;

public static class OcrHelper
{
    private class TopBottomPositions
    {
        public double Top { get; set; }
        
        public double Bottom { get; set; }
    }
    
    public static IReadOnlyList<DocumentLine> Group(
        IReadOnlyList<LineAndWords> returnLines,
        bool wordPerLine,
        int pageNumber,
        int lineHeight_OLDFLOWONLY,
        int horizontalColumnGapTrigger,
        int minimumFontSize,
        int maxDiffPercentLineHeight_OLDFLOWONLY,
        int maxNegativeDiffBetweenWordTop_OLDFLOWONLY,
        int maxPositiveDiffBetweenWordTop_OLDFLOWONLY,
        int considerableOverlapAmount,
        int maxYDiffToCombine)
    {
        const int unacceptableIncorrectValue = 80;
        var lineNumber = 0;
        
        LineAndWords? previousLine = null;
        var lineIndex = 0;
        
        // BoundingBox is { 0 X top left, 1 Y top left , 2 X top right , 3 Y top right,
        // 4 X bottom right , 5 Y bottom right , 6 X bottom left , 7 Y bottom left }

        //wordPerLine = true;
        
        var rawLines = returnLines
            .Where(line => wordPerLine ?
                !string.IsNullOrEmpty(line.Text)
                : !FormattingHelper.IsNullOrEmptyWhitespaceOrPunctuation(line.Text))
            .Where(line => wordPerLine || !DataHelper.IsCorruptedLine(line.Words, unacceptableIncorrectValue))
            .ToList();

        if (wordPerLine)
        {
            // 0. Autocorrect + remove corrupted
            var autoCorrectedWords = rawLines
                .Where(line => !string.IsNullOrEmpty(line.Text))
                .SelectMany(line => line.Words!)
                .Select(AutoCorrectHelper.ReplaceSomeSpecialCharacters)
                .Where(word => !string.IsNullOrEmpty(word?.Text))
                .Select(AutoCorrectHelper.AutoCorrectWordIfNecessary)
                .Where(word => !DataHelper.IsCorruptedWord(word))
                .ToList();
            
            // 0b. Remove tiny words

            var correctSizedWords = new List<DocumentLineWord>();

            foreach (var word in autoCorrectedWords)
            {
                if (word == null)
                {
                    continue;
                }
                
                var wordHeight = word.Coordinates.Bottom - word.Coordinates.Top;

                if (minimumFontSize > wordHeight)
                {    
                    continue;
                }
                
                correctSizedWords.Add(word);
            }
            
            // 1. Order broadly by vertical then horizontal position
            
            var naiveOrderedWords = correctSizedWords
                .OrderBy(word => word.Coordinates.Top)
                .ThenBy(word => word.Coordinates.Left)
                .ToList();

            DocumentLineWord? previousWord = null;
            
            var verticalWordGridDict = new Dictionary<TopBottomPositions, List<DocumentLineWord>>();
            
            // 2. Fit into a grid based dictionary, by checking how words overlap with existing lines
            
            foreach (var word in naiveOrderedWords)
            {
                var wordTop = word.Coordinates.Top;
                var wordBottom = word.Coordinates.Bottom;

                var positions = new TopBottomPositions
                {
                    Top = wordTop,
                    Bottom = wordBottom
                };
                
                if (previousWord == null)
                {
                    verticalWordGridDict.Add(positions, [word]);
                    previousWord = word;
                    
                    continue;
                }

                if (word.Coordinates.Top > 780 && word.Text.Contains("WATER", StringComparison.InvariantCultureIgnoreCase) == true)
                {
                    
                }

                var overlapsWithLine = verticalWordGridDict
                    .Where(gridLine => gridLine.Key.Top + 100 > word.Coordinates.Top)
                    .FirstOrDefault(gridLine =>
                    {
                        var gridOrderedWords = gridLine
                            .Value
                            .OrderBy(w => w.Coordinates.Left)
                            .ToList();
                        
                        var previousHorizontalWord = gridOrderedWords
                            .LastOrDefault(w => w.Coordinates.Right < word.Coordinates.Left);
                        
                        var nextHorizontalWord = gridOrderedWords
                            .FirstOrDefault(w => w.Coordinates.Left > word.Coordinates.Right);

                        var siblingWord = previousHorizontalWord ?? nextHorizontalWord;

                        if (siblingWord?.Text.Equals("Abstracted", StringComparison.InvariantCultureIgnoreCase) == true)
                        {
                            
                        }
                        
                        if (siblingWord == null)
                        {
                            return false;
                        }
                        
                        var siblingTop = siblingWord.Coordinates.Top;
                        var siblingBottom = siblingWord.Coordinates.Bottom;
                        
                        var wordTopOverlaps = wordTop >= siblingTop
                            && wordTop <= siblingBottom;

                        var wordBottomOverlaps = wordBottom >= siblingTop
                            && wordBottom <= siblingBottom;
                        
                        // Font is smaller, but fully enclosed in the line
                        if (wordTopOverlaps && wordBottomOverlaps)
                        {
                            return true;
                        }

                        var wordTopConsiderablyOverlaps = wordTop + considerableOverlapAmount >= siblingTop
                            && wordTop + considerableOverlapAmount <= siblingBottom;
                        
                        // Word is slightly futher down the page, or font is bigger
                        if (wordTopOverlaps && wordTopConsiderablyOverlaps && wordBottom >= siblingBottom)
                        {
                            const int considerableAmount = 10;
                            
                            var wouldOverlapWordConsiderably = gridOrderedWords
                                .Any(gow => word.Coordinates.Left >= gow.Coordinates.Left
                                    && word.Coordinates.Left + considerableAmount <= gow.Coordinates.Right);

                            if (word.Text == "day")
                            {
                            }

                            if (wouldOverlapWordConsiderably)
                            {
                                return false;
                            }
                            
                            return true;
                        }

                        // Word starts above the line, but goes into it
                        if (wordBottomOverlaps && wordTop < siblingBottom)
                        {
                            return true;
                        }

                        return false;
                    });

                if (overlapsWithLine.Value != null)
                {
                    overlapsWithLine.Value.Add(word);
                    var orderedWords = overlapsWithLine
                        .Value
                        .OrderBy(w => w.Coordinates.Left)
                        .ToList();

                    overlapsWithLine.Value.Clear();
                    overlapsWithLine.Value.AddRange(orderedWords);
                    
                    var firstWord = orderedWords.First();
                    overlapsWithLine.Key.Top = firstWord.Coordinates.Top;
                    overlapsWithLine.Key.Bottom = firstWord.Coordinates.Bottom;
                        
                    previousWord = word;
                    continue;
                }
                
                verticalWordGridDict.Add(positions, [word]);
                previousWord = word;
            }

            // 3. Order each line to produce ordered words per line
            
            var orderedLines = new List<DocumentLine>();
            
            foreach (var kvp in verticalWordGridDict)
            {
                var orderedWords = kvp.Value
                    .OrderBy(word => word.Coordinates.Left)
                    .ToList();

                var columns = new List<DocumentLineColumn>
                {
                    new()
                };

                previousWord = null;
                
                foreach (var word in orderedWords)
                {
                    if (previousWord != null)
                    {
                        var horizontalGapFromPreviousWord = word.Coordinates.Left - previousWord.Coordinates.Right;

                        if (horizontalGapFromPreviousWord > horizontalColumnGapTrigger)
                        {
                            columns.Add(new DocumentLineColumn());
                        }
                    }
                    
                    var column = columns.Last();
                    column.Words.Add(word);

                    previousWord = word;
                }

                foreach (var column in columns)
                {
                    column.Text = string.Join(' ', column.Words.Select(w => w.Text));
                }
                
                var firstWordCoords = orderedWords.First().Coordinates;
                
                var documentLine = new DocumentLine(
                    lineNumber++,
                    pageNumber,
                    columns,
                    firstWordCoords.Top,
                    firstWordCoords.Right,
                    firstWordCoords.Bottom,
                    firstWordCoords.Left);
                
                orderedLines.Add(documentLine);
            }
            
            // 4. Move words down if they would fit better there

            var combinedLines = new List<DocumentLine>();

            var lineCount = 0;
            var totalLines = orderedLines.Count;
            
            foreach (var line in orderedLines)
            {
                if (lineCount == totalLines - 1)
                {
                    combinedLines.Add(line);
                    continue;
                }
                
                var nextLine = orderedLines[lineCount + 1];
                var allNextLineWords = new List<(DocumentLineColumn Column, DocumentLineWord Word)>();

                foreach (var nextLineColumn in nextLine.Columns)
                {
                    foreach (var nextLineColumnWord in nextLineColumn.Words)
                    {
                        allNextLineWords.Add((nextLineColumn, nextLineColumnWord));
                    }
                }
                
                var allLineWords = line.Columns
                    .SelectMany(column => column.Words)
                    .ToList();
                
                var totalWords = line.Columns
                    .Sum(column => column.Words.Count);
                
                previousWord = null;
                var wordCount = 0;
                
                foreach (var column in line.Columns)
                {
                    var newWords = new List<DocumentLineWord>();
                    
                    foreach (var word in column.Words)
                    {
                        if (word.Text.Equals("DURING", StringComparison.InvariantCultureIgnoreCase))
                        {
                            
                        }
                        
                        var nextWord = wordCount + 1 < totalWords ? allLineWords[wordCount + 1] : null;

                        var xDiffToPreviousWord = previousWord?.Coordinates.Right - word.Coordinates.Left;
                        var xDiffToNextWord = nextWord?.Coordinates.Left - word.Coordinates.Right;

                        var closestSibling = previousWord == null
                            || xDiffToPreviousWord > xDiffToNextWord ? nextWord : previousWord;
                        
                        DocumentLineWord? nextLineClosestSibling = null;
                        DocumentLineColumn? nextLineColumnClosestSibling = null;
                        
                        double nextLineSiblingXDiff = int.MaxValue;

                        foreach (var nextLineWordTuple in allNextLineWords)
                        {
                            var nextLineWord = nextLineWordTuple.Word;
                            double d;

                            if (nextLineWord.Coordinates.Right < word.Coordinates.Left)
                            {
                                d = word.Coordinates.Left - nextLineWord.Coordinates.Right;
                            }
                            else
                            {
                                d = nextLineWord.Coordinates.Left - word.Coordinates.Right;
                            }

                            if (d < nextLineSiblingXDiff)
                            {
                                nextLineSiblingXDiff = d;
                                
                                nextLineClosestSibling = nextLineWord;
                                nextLineColumnClosestSibling = nextLineWordTuple.Column;
                            }
                        }
                        
                        var siblingYDiff = GetMidpoint(closestSibling?.Coordinates) - GetMidpoint(word.Coordinates);
                        var nextLineSiblingYDiff = GetMidpoint(nextLineClosestSibling?.Coordinates) - GetMidpoint(word.Coordinates);

                        const int worthwhileYDifference = 5;
                        
                        if (siblingYDiff != null && nextLineSiblingYDiff != null)
                        {
                            if (Math.Abs(siblingYDiff.Value) > Math.Abs(nextLineSiblingYDiff.Value)
                                && Math.Abs(siblingYDiff.Value) - Math.Abs(nextLineSiblingYDiff.Value) > worthwhileYDifference)
                            {
                                // Move it to the next row then
                                nextLineColumnClosestSibling!.Words.Add(word);
                                nextLineColumnClosestSibling.Words = nextLineColumnClosestSibling.Words
                                    .OrderBy(w => w.Coordinates.Left).ToList();
                                
                                nextLineColumnClosestSibling.Text = string.Join(' ', nextLineColumnClosestSibling.Words.Select(w => w.Text));
                            }
                            else
                            {
                                newWords.Add(word);
                            }
                        }
                        else if (nextLineSiblingYDiff != null && Math.Abs(nextLineSiblingYDiff.Value) < worthwhileYDifference)
                        {
                            // Move it to the next row then
                            nextLineColumnClosestSibling!.Words.Add(word);
                            nextLineColumnClosestSibling.Words = nextLineColumnClosestSibling.Words
                                .OrderBy(w => w.Coordinates.Left).ToList();
                            
                            nextLineColumnClosestSibling.Text = string.Join(' ', nextLineColumnClosestSibling.Words.Select(w => w.Text));
                        }
                        else
                        {
                            newWords.Add(word);
                        }
                        
                        // ... Do something
                        
                        previousWord = word;
                        wordCount += 1;
                    }

                    column.Words = newWords;
                    column.Text = string.Join(' ', column.Words.Select(w => w.Text));
                }

                combinedLines.Add(line);
                lineCount += 1;
            }

            var combinedLinesNoBlanks = combinedLines
                .Where(line => !FormattingHelper.IsNullOrEmptyWhitespaceOrPunctuation(line.Text))
                .ToList();
            
            // TODO - another pass to look for pointlessly short lines? ones without any values on or just a floating number?
            
            return combinedLinesNoBlanks;
        }
        
        var groupedLines = rawLines
            .GroupBy(line =>
            {
                previousLine ??= line;

                var yDiff = GetMidpoint(line.Words![0]!.Coordinates)
                    - GetMidpoint(previousLine?.Words![0]!.Coordinates);

                if (yDiff > lineHeight_OLDFLOWONLY)
                {
                    lineIndex += 1;
                }

                previousLine = line;
                return lineIndex;
            })
            .Select(lines =>
            {
                var words = new List<DocumentLineWord>();
                DocumentLineWord? previousOkWord = null;
                
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
                        
                        if (minimumFontSize > wordHeight)
                        {
                            continue;
                        }
                        
                        var wordTextWithoutPunctuation = word.Text
                            .Replace(",", string.Empty)
                            .Replace(".", string.Empty)
                            .Replace(";", string.Empty)
                            .Replace("'", string.Empty)
                            .Replace("\"", string.Empty);                        
                        
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
                                    lineWords.Add(
                                        new DocumentLineWord(
                                            topSuggestion,
                                            word.OcrConfidence,
                                            word.Coordinates,
                                            word.HandwrittenOrTyped));
                                    
                                    previousOkWord = word;                                    
                                    continue;
                                }
                            }
                        }
                        
                        previousOkWord = word;                        
                        lineWords.Add(word);
                    }
                    
                    words.AddRange(lineWords);
                }

                var columns = new List<DocumentLineColumn>
                {
                    new()
                };
                
                previousOkWord = null;
                
                foreach (var word in words.OrderBy(w => w.Coordinates.Left))
                {
                    var diffBetweenTops = word.Coordinates.Top - previousOkWord?.Coordinates.Top;

                    if (previousOkWord != null && diffBetweenTops > 0 && diffBetweenTops > maxPositiveDiffBetweenWordTop_OLDFLOWONLY)
                    {
                        continue;
                    }
                    
                    if (previousOkWord != null && diffBetweenTops < 0 && diffBetweenTops < maxNegativeDiffBetweenWordTop_OLDFLOWONLY)
                    {
                        continue;
                    }
                 
                    var previousWordHeight = previousOkWord?.Coordinates.Bottom - previousOkWord?.Coordinates.Top;
                    var wordHeight = word!.Coordinates.Bottom - word.Coordinates.Top;
                    var percentOfPrevious = previousOkWord != null ?
                        GetPercentOfPrevious(previousWordHeight!.Value, wordHeight)
                        : null;
                    
                    if (previousOkWord != null && percentOfPrevious < maxDiffPercentLineHeight_OLDFLOWONLY)
                    {
                        continue;
                    }
                    
                    var xDiff = word.Coordinates.Left - previousOkWord?.Coordinates.Right;
                    
                    if (xDiff > horizontalColumnGapTrigger)
                    {
                        columns.Add(new DocumentLineColumn());
                    }

                    var column = columns.Last();
                    column.Words.Add(word);

                    previousOkWord = word;
                }

                foreach (var column in columns)
                {
                    column.Text = string.Join(' ', column.Words.Select(w => w.Text));
                }
                
                var firstWordCoords = columns.FirstOrDefault()?.Words.FirstOrDefault()?.Coordinates;
                
                var documentLine = new DocumentLine(
                    lineNumber++,
                    pageNumber,
                    columns,
                    firstWordCoords?.Top ?? PositionConstants.UnknownCoordinate,
                    firstWordCoords?.Right ?? PositionConstants.UnknownCoordinate,
                    firstWordCoords?.Bottom ?? PositionConstants.UnknownCoordinate,
                    firstWordCoords?.Left ?? PositionConstants.UnknownCoordinate);

                return documentLine;
            })
            .Where(line => !DataHelper.IsCorruptedText(line.Text, false, unacceptableIncorrectValue))
            .ToList();

        return groupedLines;
    }

    private static double? GetPercentOfPrevious(double previousWordHeight, double wordHeight)
    {
        var percentPerCharacter = 100.0 / previousWordHeight;
        var percentOfPrevious = percentPerCharacter * wordHeight;
        
        return percentOfPrevious;
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