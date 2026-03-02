using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OutputSchema;

namespace WALE.ProcessFile.Core.Helpers;

public static class OcrHelper
{
    public static async Task<IReadOnlyList<DocumentLine>> GroupAsync(
        IReadOnlyList<LineAndWords> returnLines,
        bool useNewProcessingFlow,
        int pageNumber,
        int horizontalColumnGapTrigger,
        int minimumFontSize,
        int considerableOverlapAmount,
        int lineHeightLegacyFlowOnly = -1,
        int maxDiffPercentFontSizeLegacyFlowOnly = -1,
        int maxNegativeDiffBetweenWordTopLegacyFlowOnly = -1,
        int maxPositiveDiffBetweenWordTopLegacyFlowOnly = -1)
    {
        AutoCorrectHelper.RemoveSpacesAroundSlashes(returnLines);
        
        if (!useNewProcessingFlow)
        {
            return await LegacyGroupingAsync(
                returnLines,
                pageNumber,
                horizontalColumnGapTrigger,
                minimumFontSize,
                lineHeightLegacyFlowOnly,
                maxDiffPercentFontSizeLegacyFlowOnly,
                maxNegativeDiffBetweenWordTopLegacyFlowOnly,
                maxPositiveDiffBetweenWordTopLegacyFlowOnly);
        }
        
        var lineNumber = 0;

        // BoundingBox is { 0 X top left, 1 Y top left , 2 X top right , 3 Y top right,
        // 4 X bottom right , 5 Y bottom right , 6 X bottom left , 7 Y bottom left }

        var rawLines = returnLines
            .Where(line => !string.IsNullOrEmpty(line.Text))
            .ToList();

        // 0. Autocorrect + remove corrupted
        var removedSpecialCharacterAndEmptyWords = rawLines
            .Where(line => !string.IsNullOrEmpty(line.Text))
            .SelectMany(line => line.Words!)
            .Select(AutoCorrectHelper.ReplaceSomeSpecialCharacters)
            .Where(word => !string.IsNullOrEmpty(word?.Text))
            .ToList();

        var correctWordTasks = new List<Task<DocumentLineWord?>>();

        foreach (var word in removedSpecialCharacterAndEmptyWords)
        {
            var task = Task.Run(() =>
                AutoCorrectHelper.AutoCorrectWordIfNecessary(word));
            
            correctWordTasks.Add(task);
        }

        var correctedWords = new List<DocumentLineWord?>();
        
        foreach (var task in correctWordTasks)
        {
            correctedWords.Add(await task);
        }
        
        var checkForCorruptedTasks = new List<Task<(bool Corrupted, DocumentLineWord? Word)>>();
        
        foreach (var word in correctedWords)
        {
            var task = Task.Run(() =>
                (DataHelper.IsCorruptedWord(word, true), word));
            
            checkForCorruptedTasks.Add(task);
        }
        
        var autoCorrectedWords = new List<DocumentLineWord?>();
        
        foreach (var task in checkForCorruptedTasks)
        {
            var (corrupted, word) = await task;

            if (corrupted)
            {
                continue;
            }

            autoCorrectedWords.Add(word);
        }
        
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

                        return !wouldOverlapWordConsiderably;
                    }

                    // Word starts above the line, but goes into it
                    return wordBottomOverlaps && wordTop < siblingBottom;
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
                    var nextWord = wordCount + 1 < totalWords ? allLineWords[wordCount + 1] : null;

                    var xDiffToPreviousWord = previousWord?.Coordinates.Right - word.Coordinates.Left;
                    var xDiffToNextWord = nextWord?.Coordinates.Left - word.Coordinates.Right;

                    var closestSibling = previousWord == null
                        || xDiffToPreviousWord > xDiffToNextWord ? nextWord : previousWord;
                    
                    DocumentLineWord? nextLineClosestSibling = null;
                    DocumentLineColumn? nextLineColumnClosestSibling = null;
                    
                    double nextLineSiblingXDiff = int.MaxValue;

                    foreach (var (documentLineColumn, nextLineWord) in allNextLineWords)
                    {
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
                            nextLineColumnClosestSibling = documentLineColumn;
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
                    }
                    else
                    {
                        newWords.Add(word);
                    }
                    
                    // ... TODO Do something
                    
                    previousWord = word;
                    wordCount += 1;
                }

                column.Words = newWords;
            }

            combinedLines.Add(line);
            lineCount += 1;
        }
        
        // 5. Check size of each word against its siblings - remove if its too small
        foreach (var line in combinedLines)
        {
            foreach (var column in line.Columns)
            {
                DocumentLineWord? previousOkWord = null;
                var anyColumnChange = false;
                var newWordList = new List<DocumentLineWord>();
                
                foreach (var word in column.Words)
                {
                    var previousWordHeight = previousOkWord?.Coordinates.Bottom - previousOkWord?.Coordinates.Top;
                    var wordHeight = word.Coordinates.Bottom - word.Coordinates.Top;
                    
                    var percentOfPrevious = previousOkWord != null ?
                        GetPercentOfPrevious(previousWordHeight!.Value, wordHeight)
                        : null;
                
                    if (previousOkWord != null
                        && percentOfPrevious < maxDiffPercentFontSizeLegacyFlowOnly)
                    {
                        anyColumnChange = true;
                        continue;
                    }
                    
                    newWordList.Add(word);
                    previousOkWord = word;
                }

                if (anyColumnChange)
                {
                    column.Words = newWordList;
                }
            }
        }

        var combinedLinesNoBlanks = combinedLines
            .Where(line => !FormattingHelper.IsNullOrEmptyWhitespaceOrPunctuation(line.Text))
            .ToList();

        var previousTop = (double?)null;
        const int maxHeightDiff = 200;
        var returnList = new List<DocumentLine>();
        
        // Add in some empty seperator lines where appropriate
        foreach (var line in combinedLinesNoBlanks)
        {
            if (previousTop != null && line.Top - previousTop > maxHeightDiff)
            {
                returnList.Add(new DocumentLine());
            }
            
            returnList.Add(line);
            previousTop = line.Top;
        }
        
        // TODO - another pass to look for pointlessly short lines? ones without any values on or just a floating number?
        
        return returnList;
    }

    private static async Task<IReadOnlyList<DocumentLine>> LegacyGroupingAsync(
        IReadOnlyList<LineAndWords> inputLines,
        int pageNumber,
        int horizontalColumnGapTrigger,
        int minimumFontSize,
        int lineHeight,
        int maxDiffPercentFontSize,
        int maxNegativeDiffBetweenWordTop,
        int maxPositiveDiffBetweenWordTop)
    {
        const int unacceptableIncorrectValue = 80;
        var lineNumber = 0;

        LineAndWords? previousLine = null;
        var lineIndex = 0;

        // BoundingBox is { 0 X top left, 1 Y top left , 2 X top right , 3 Y top right,
        // 4 X bottom right , 5 Y bottom right , 6 X bottom left , 7 Y bottom left }

        var noneCorruptOrEmptyLines = inputLines
            .Where(line => !FormattingHelper.IsNullOrEmptyWhitespaceOrPunctuation(line.Text))
            .Where(line => !DataHelper.IsCorruptedLine(line.Words, true, unacceptableIncorrectValue))
            .ToList();

        var yGroupedLines = noneCorruptOrEmptyLines
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
            .ToList();

        var groupedLines = new List<DocumentLine>();
        
        foreach (var lines in yGroupedLines)
        {
            var wordTasks = new List<Task<DocumentLineWord?>>();

            foreach (var line in lines.OrderBy(l => l.Words![0]!.Coordinates.Left))
            {
                if (line.Words == null)
                {
                    continue;
                }

                foreach (var word in line.Words)
                {
                    wordTasks.Add(CorrectWord(word!, minimumFontSize));
                }
            }

            var words = new List<DocumentLineWord>();

            foreach (var wordTask in wordTasks)
            {
                var word = await wordTask;

                if (word != null)
                {
                    words.Add(word);
                }
            }

            var columns = new List<DocumentLineColumn>
            {
                new()
            };

            DocumentLineWord? previousOkWord = null;

            foreach (var word in words.OrderBy(w => w.Coordinates.Left))
            {
                var diffBetweenTops = word.Coordinates.Top - previousOkWord?.Coordinates.Top;

                if (previousOkWord != null && diffBetweenTops > 0 && diffBetweenTops > maxPositiveDiffBetweenWordTop)
                {
                    continue;
                }

                if (previousOkWord != null && diffBetweenTops < 0 && diffBetweenTops < maxNegativeDiffBetweenWordTop)
                {
                    continue;
                }

                var previousWordHeight = previousOkWord?.Coordinates.Bottom - previousOkWord?.Coordinates.Top;
                var wordHeight = word.Coordinates.Bottom - word.Coordinates.Top;
                var percentOfPrevious = previousOkWord != null
                    ? GetPercentOfPrevious(previousWordHeight!.Value, wordHeight)
                    : null;

                if (previousOkWord != null && percentOfPrevious < maxDiffPercentFontSize)
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

            var firstWordCoords = columns.FirstOrDefault()?.Words.FirstOrDefault()?.Coordinates;

            var documentLine = new DocumentLine(
                lineNumber++,
                pageNumber,
                columns,
                firstWordCoords?.Top ?? PositionConstants.UnknownCoordinate,
                firstWordCoords?.Right ?? PositionConstants.UnknownCoordinate,
                firstWordCoords?.Bottom ?? PositionConstants.UnknownCoordinate,
                firstWordCoords?.Left ?? PositionConstants.UnknownCoordinate);

            if (!DataHelper.IsCorruptedText(documentLine.Text, true, unacceptableIncorrectValue))
            {
                groupedLines.Add(documentLine);   
            }
        }
    
        return groupedLines;
    }
    
    private static Task<DocumentLineWord?> CorrectWord(
        DocumentLineWord word,
        double minimumFontSize)
    {
        var wordHeight = word.Coordinates.Bottom - word.Coordinates.Top;
                        
        if (minimumFontSize > wordHeight)
        {
            return Task.FromResult((DocumentLineWord?)null);
        }
    
        var wordTextWithoutPunctuation = word.Text
            .Replace(",", string.Empty)
            .Replace(".", string.Empty)
            .Replace(";", string.Empty)
            .Replace("'", string.Empty)
            .Replace("\"", string.Empty);
        
        if (word.OcrConfidence < 40
            && word.Text.Length > 3
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
                    return Task.FromResult(new DocumentLineWord(
                        topSuggestion,
                        word.OcrConfidence,
                        word.Coordinates,
                        word.HandwrittenOrTyped))!;
                }
            }
        }

        return Task.FromResult(word)!;
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
    
    public static bool IsPageScreenshot(string imageReference, int pageNumber)
    {
        var imageReferenceLower = imageReference.ToLower();
        
        return 
            imageReferenceLower.StartsWith("screenshot")
            || imageReferenceLower.EndsWith($"page-{pageNumber}.jpg")
            || imageReferenceLower.EndsWith($"page-{pageNumber}.png");
    }
    
    private class TopBottomPositions
    {
        public double Top { get; set; }
        
        public double Bottom { get; set; }
    }
}