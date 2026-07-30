using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Core.Tests.UnitTests;

public class LabelMatchingHelperTests
{
    [Fact]
    public void WhenSingleLineWith3WordsInput_WhenSimpleLabelToFind_ThenFindsFirstWord()
    {
        // Arrange
        var line1 = SingleColumn3Words();
        var line2 = (DocumentLine?)null;
        var line1ForPosition = line1.Clone();
        var labelTextToMatch = new List<TextToMatch>
        {
            new("A")
        };
        var labelPosition = LabelPosition.TextToFindIsBetweenLabels;
        var lineCount = 1;
        var howManyLinesTotal = 2;
        
        // Act
        var result = LabelMatchingHelper.LineContainsLabel(
            line1,
            line2,
            line1ForPosition,
            labelTextToMatch,
            labelPosition,
            lineCount,
            howManyLinesTotal,
            out var matchedText,
            out var labelStartPageNumber,
            out var labelStartLineNumber,
            out var labelStartCharIndex,
            out var labelEndPageNumber,
            out var labelEndLineNumber,
            out var labelEndCharIndex);

        // Assert
        Assert.True(result);
        Assert.NotNull(matchedText);
        Assert.Equal(new TextToMatch("A").Text, matchedText.Text);
        Assert.Equal(2, labelStartPageNumber);
        Assert.Equal(1, labelStartLineNumber);
        Assert.Equal(0, labelStartCharIndex);
        Assert.Equal(2, labelEndPageNumber);
        Assert.Equal(1, labelEndLineNumber);
        Assert.Equal(1, labelEndCharIndex);
    }

    [Fact]
    public void WhenSingleLineWith3WordsInput_WhenSimpleLabelToFind_ThenFindsMiddleWord()
    {
        // Arrange
        var line1 = SingleColumn3Words();
        var line2 = (DocumentLine?)null;
        var line1ForPosition = line1.Clone();
        var labelTextToMatch = new List<TextToMatch>
        {
            new("B")
        };
        var labelPosition = LabelPosition.TextToFindIsBetweenLabels;
        var lineCount = 1;
        var howManyLinesTotal = 2;
        
        // Act
        var result = LabelMatchingHelper.LineContainsLabel(
            line1,
            line2,
            line1ForPosition,
            labelTextToMatch,
            labelPosition,
            lineCount,
            howManyLinesTotal,
            out var matchedText,
            out var labelStartPageNumber,
            out var labelStartLineNumber,
            out var labelStartCharIndex,
            out var labelEndPageNumber,
            out var labelEndLineNumber,
            out var labelEndCharIndex);

        // Assert
        Assert.True(result);
        Assert.NotNull(matchedText);
        Assert.Equal(new TextToMatch("B").Text, matchedText.Text);
        Assert.Equal(2, labelStartPageNumber);
        Assert.Equal(1, labelStartLineNumber);
        Assert.Equal(2, labelStartCharIndex);
        Assert.Equal(2, labelEndPageNumber);
        Assert.Equal(1, labelEndLineNumber);
        Assert.Equal(3, labelEndCharIndex);
    }
    
    [Fact]
    public void WhenSingleLineWith3WordsInput_WhenSimpleLabelToFind_ThenFindsLastWord()
    {
        // Arrange
        var line1 = SingleColumn3Words();
        var line2 = (DocumentLine?)null;
        var line1ForPosition = line1.Clone();
        var labelTextToMatch = new List<TextToMatch>
        {
            new("C")
        };
        var labelPosition = LabelPosition.TextToFindIsBetweenLabels;
        var lineCount = 1;
        var howManyLinesTotal = 2;
        
        // Act
        var result = LabelMatchingHelper.LineContainsLabel(
            line1,
            line2,
            line1ForPosition,
            labelTextToMatch,
            labelPosition,
            lineCount,
            howManyLinesTotal,
            out var matchedText,
            out var labelStartPageNumber,
            out var labelStartLineNumber,
            out var labelStartCharIndex,
            out var labelEndPageNumber,
            out var labelEndLineNumber,
            out var labelEndCharIndex);

        // Assert
        Assert.True(result);
        Assert.NotNull(matchedText);
        Assert.Equal(new TextToMatch("C").Text, matchedText.Text);
        Assert.Equal(2, labelStartPageNumber);
        Assert.Equal(1, labelStartLineNumber);
        Assert.Equal(4, labelStartCharIndex);
        Assert.Equal(2, labelEndPageNumber);
        Assert.Equal(1, labelEndLineNumber);
        Assert.Equal(5, labelEndCharIndex);
    }
    
    [Fact]
    public void WhenSingleLineWith3WordsInput_WhenLabelToFindAtEndOfColumn_ThenFindsLastWord()
    {
        // Arrange
        var line1 = SingleColumn3Words();        
        var line2 = (DocumentLine?)null;
        var line1ForPosition = line1.Clone();
        var labelTextToMatch = new List<TextToMatch>
        {
            new("C[END_OF_COLUMN]")
        };
        var labelPosition = LabelPosition.TextToFindIsBetweenLabels;
        var lineCount = 1;
        var howManyLinesTotal = 2;
        
        // Act
        var result = LabelMatchingHelper.LineContainsLabel(
            line1,
            line2,
            line1ForPosition,
            labelTextToMatch,
            labelPosition,
            lineCount,
            howManyLinesTotal,
            out var matchedText,
            out var labelStartPageNumber,
            out var labelStartLineNumber,
            out var labelStartCharIndex,
            out var labelEndPageNumber,
            out var labelEndLineNumber,
            out var labelEndCharIndex);

        // Assert
        Assert.True(result);
        Assert.NotNull(matchedText);
        Assert.Equal(new TextToMatch("C").Text, matchedText.Text);
        Assert.Equal(2, labelStartPageNumber);
        Assert.Equal(1, labelStartLineNumber);
        Assert.Equal(4, labelStartCharIndex);
        Assert.Equal(2, labelEndPageNumber);
        Assert.Equal(1, labelEndLineNumber);
        Assert.Equal(5, labelEndCharIndex);
    }

    [Fact]
    public void WhenSingleLineWith3WordsInputSingleColumn_WhenLabelToFindAtEndOfLine_ThenFindsLastWord()
    {
        // Arrange
        var line1 = SingleColumn3Words();
        var line2 = (DocumentLine?)null;
        var line1ForPosition = line1.Clone();
        var labelTextToMatch = new List<TextToMatch>
        {
            new("C[END_OF_LINE]")
        };
        var labelPosition = LabelPosition.TextToFindIsBetweenLabels;
        var lineCount = 1;
        var howManyLinesTotal = 2;
        
        // Act
        var result = LabelMatchingHelper.LineContainsLabel(
            line1,
            line2,
            line1ForPosition,
            labelTextToMatch,
            labelPosition,
            lineCount,
            howManyLinesTotal,
            out var matchedText,
            out var labelStartPageNumber,
            out var labelStartLineNumber,
            out var labelStartCharIndex,
            out var labelEndPageNumber,
            out var labelEndLineNumber,
            out var labelEndCharIndex);

        // Assert
        Assert.True(result);
        Assert.NotNull(matchedText);
        Assert.Equal(new TextToMatch("C").Text, matchedText.Text);
        Assert.Equal(2, labelStartPageNumber);
        Assert.Equal(1, labelStartLineNumber);
        Assert.Equal(4, labelStartCharIndex);
        Assert.Equal(2, labelEndPageNumber);
        Assert.Equal(1, labelEndLineNumber);
        Assert.Equal(5, labelEndCharIndex);
    }
    
    [Fact]
    public void WhenSingleLineWith3WordsInputMultipleColumns_WhenLabelToFindAtEndOfLine_ThenFindsLastWord()
    {
        // Arrange
        var line1 = TwoColumns3Words();
        var line2 = (DocumentLine?)null;
        var line1ForPosition = line1.Clone();
        var labelTextToMatch = new List<TextToMatch>
        {
            new("C[END_OF_LINE]")
        };
        var labelPosition = LabelPosition.TextToFindIsBetweenLabels;
        var lineCount = 1;
        var howManyLinesTotal = 2;
        
        // Act
        var result = LabelMatchingHelper.LineContainsLabel(
            line1,
            line2,
            line1ForPosition,
            labelTextToMatch,
            labelPosition,
            lineCount,
            howManyLinesTotal,
            out var matchedText,
            out var labelStartPageNumber,
            out var labelStartLineNumber,
            out var labelStartCharIndex,
            out var labelEndPageNumber,
            out var labelEndLineNumber,
            out var labelEndCharIndex);

        // Assert
        Assert.True(result);
        Assert.NotNull(matchedText);
        Assert.Equal(new TextToMatch("C").Text, matchedText.Text);
        Assert.Equal(2, labelStartPageNumber);
        Assert.Equal(1, labelStartLineNumber);
        Assert.Equal(4, labelStartCharIndex);
        Assert.Equal(2, labelEndPageNumber);
        Assert.Equal(1, labelEndLineNumber);
        Assert.Equal(5, labelEndCharIndex);
    }
    
    [Fact]
    public void WhenSingleLineWith3WordsInputMultipleColumns_WhenLabelToFindAtEndOfLineButWordNot_ThenDoesntFindMatch()
    {
        // Arrange
        var line1 = TwoColumns3Words();
        var line2 = (DocumentLine?)null;
        var line1ForPosition = line1.Clone();
        var labelTextToMatch = new List<TextToMatch>
        {
            new("B[END_OF_LINE]")
        };
        var labelPosition = LabelPosition.TextToFindIsBetweenLabels;
        var lineCount = 1;
        var howManyLinesTotal = 2;
        
        // Act
        var result = LabelMatchingHelper.LineContainsLabel(
            line1,
            line2,
            line1ForPosition,
            labelTextToMatch,
            labelPosition,
            lineCount,
            howManyLinesTotal,
            out var matchedText,
            out _,
            out _,
            out _,
            out _,
            out _,
            out _);

        // Assert
        Assert.False(result);
        Assert.Null(matchedText);
    }
    
    [Fact]
    public void WhenSingleLineWith3WordsInputFirstLine_WhenLookingForStartOfBlock_ThenFindsStartOfBlock()
    {
        // Arrange
        var line1 = SingleColumn3Words();
        var line2 = (DocumentLine?)null;
        var line1ForPosition = line1.Clone();
        var labelTextToMatch = new List<TextToMatch>
        {
            new("[START_OF_BLOCK]")
        };
        var labelPosition = LabelPosition.TextToFindIsBetweenLabels;
        var lineIndex = 0;
        var howManyLinesTotal = 2;
        
        // Act
        var result = LabelMatchingHelper.LineContainsLabel(
            line1,
            line2,
            line1ForPosition,
            labelTextToMatch,
            labelPosition,
            lineIndex,
            howManyLinesTotal,
            out var matchedText,
            out var labelStartPageNumber,
            out var labelStartLineNumber,
            out var labelStartCharIndex,
            out var labelEndPageNumber,
            out var labelEndLineNumber,
            out var labelEndCharIndex);

        // Assert
        Assert.True(result);
        Assert.NotNull(matchedText);
        Assert.Equal(new TextToMatch("[START_OF_BLOCK]").Text, matchedText.Text);
        Assert.Equal(2, labelStartPageNumber);
        Assert.Equal(1, labelStartLineNumber);
        Assert.Equal(0, labelStartCharIndex);
        Assert.Equal(2, labelEndPageNumber);
        Assert.Equal(1, labelEndLineNumber);
        Assert.Equal(0, labelEndCharIndex);
    }
    
    [Fact]
    public void WhenSingleLineWith3WordsInputSecondLine_WhenLookingForStartOfBlock_ThenDoesntFindStartOfBlock()
    {
        // Arrange
        var line1 = SingleColumn3Words();
        var line2 = (DocumentLine?)null;
        var line1ForPosition = line1.Clone();
        var labelTextToMatch = new List<TextToMatch>
        {
            new("[START_OF_BLOCK]")
        };
        var labelPosition = LabelPosition.TextToFindIsBetweenLabels;
        var lineIndex = 1;
        var howManyLinesTotal = 2;
        
        // Act
        var result = LabelMatchingHelper.LineContainsLabel(
            line1,
            line2,
            line1ForPosition,
            labelTextToMatch,
            labelPosition,
            lineIndex,
            howManyLinesTotal,
            out var matchedText,
            out _,
            out _,
            out _,
            out _,
            out _,
            out _);

        // Assert
        Assert.False(result);
        Assert.Null(matchedText);
    }
    
    [Fact]
    public void WhenTwoLinesWith3WordsInput_WhenSimpleLabelToFindAtStartOfLine_ThenFindsOverBothLine()
    {
        // Arrange
        var line1 = SingleColumn3Words();
        var line2 = SingleColumn3Words();
        var line1ForPosition = line1.Clone();
        var labelTextToMatch = new List<TextToMatch>
        {
            new("A B C A")
        };
        var labelPosition = LabelPosition.TextToFindIsBetweenLabels;
        var lineCount = 1;
        var howManyLinesTotal = 2;
        
        // Act
        var result = LabelMatchingHelper.LineContainsLabel(
            line1,
            line2,
            line1ForPosition,
            labelTextToMatch,
            labelPosition,
            lineCount,
            howManyLinesTotal,
            out var matchedText,
            out var labelStartPageNumber,
            out var labelStartLineNumber,
            out var labelStartCharIndex,
            out var labelEndPageNumber,
            out var labelEndLineNumber,
            out var labelEndCharIndex);

        // Assert
        Assert.True(result);
        Assert.NotNull(matchedText);
        Assert.Equal(new TextToMatch("A B C A").Text, matchedText.Text);
        Assert.Equal(2, labelStartPageNumber);
        Assert.Equal(1, labelStartLineNumber);
        Assert.Equal(0, labelStartCharIndex);
        Assert.Equal(2, labelEndPageNumber);
        Assert.Equal(1, labelEndLineNumber);
        Assert.Equal(1, labelEndCharIndex);
    }
    
    [Fact]
    public void WhenTwoLinesWith3WordsInput_WhenSimpleLabelToFindAtStartOfLineWithStartOfLineRestriction_ThenFindsOverBothLine()
    {
        // Arrange
        var line1 = SingleColumn3Words();
        var line2 = SingleColumn3Words();
        var line1ForPosition = line1.Clone();
        var labelTextToMatch = new List<TextToMatch>
        {
            new("A B C A")
            {
                LineMustStartWith = true
            }
        };
        var labelPosition = LabelPosition.TextToFindIsBetweenLabels;
        var lineCount = 1;
        var howManyLinesTotal = 2;
        
        // Act
        var result = LabelMatchingHelper.LineContainsLabel(
            line1,
            line2,
            line1ForPosition,
            labelTextToMatch,
            labelPosition,
            lineCount,
            howManyLinesTotal,
            out var matchedText,
            out var labelStartPageNumber,
            out var labelStartLineNumber,
            out var labelStartCharIndex,
            out var labelEndPageNumber,
            out var labelEndLineNumber,
            out var labelEndCharIndex);

        // Assert
        Assert.True(result);
        Assert.NotNull(matchedText);
        Assert.Equal(new TextToMatch("A B C A").Text, matchedText.Text);
        Assert.Equal(2, labelStartPageNumber);
        Assert.Equal(1, labelStartLineNumber);
        Assert.Equal(0, labelStartCharIndex);
        Assert.Equal(2, labelEndPageNumber);
        Assert.Equal(1, labelEndLineNumber);
        Assert.Equal(1, labelEndCharIndex);
    }
    
    [Fact]
    public void WhenTwoLinesWith3WordsInput_WhenSimpleLabelToFindNotAtStartOfLine_ThenFindsOverBothLine()
    {
        // Arrange
        var line1 = SingleColumn3Words();
        var line2 = SingleColumn3Words();
        var line1ForPosition = line1.Clone();
        var labelTextToMatch = new List<TextToMatch>
        {
            new("C A")
        };
        var labelPosition = LabelPosition.TextToFindIsBetweenLabels;
        var lineCount = 1;
        var howManyLinesTotal = 2;
        
        // Act
        var result = LabelMatchingHelper.LineContainsLabel(
            line1,
            line2,
            line1ForPosition,
            labelTextToMatch,
            labelPosition,
            lineCount,
            howManyLinesTotal,
            out var matchedText,
            out var labelStartPageNumber,
            out var labelStartLineNumber,
            out var labelStartCharIndex,
            out var labelEndPageNumber,
            out var labelEndLineNumber,
            out var labelEndCharIndex);

        // Assert
        Assert.True(result);
        Assert.NotNull(matchedText);
        Assert.Equal(new TextToMatch("C A").Text, matchedText.Text);
        Assert.Equal(2, labelStartPageNumber);
        Assert.Equal(1, labelStartLineNumber);
        Assert.Equal(4, labelStartCharIndex);
        Assert.Equal(2, labelEndPageNumber);
        Assert.Equal(1, labelEndLineNumber);
        Assert.Equal(1, labelEndCharIndex);
    }
    
    private static DocumentLine SingleColumn3Words(
        string word1 = "A",
        string word2 = "B",
        string word3 = "C")
    {
        return new DocumentLine(
            1,
            2,
            [
                new DocumentLineColumn(
                    [
                        new DocumentLineWord(
                            word1,
                            100,
                            new DocumentLineWordCoordinates(),
                            null),
                        new DocumentLineWord(
                            word2,
                            100,
                            new DocumentLineWordCoordinates(),
                            null),
                        new DocumentLineWord(
                            word3,
                            100,
                            new DocumentLineWordCoordinates(),
                            null)
                    ]
                )
            ],
            0,
            0,
            0,
            0);
    }
    
    private static DocumentLine TwoColumns3Words()
    {
        return new DocumentLine(
            1,
            2,
            [
                new DocumentLineColumn(
                    [
                        new DocumentLineWord(
                            "A",
                            100,
                            new DocumentLineWordCoordinates(),
                            null),
                        new DocumentLineWord(
                            "B",
                            100,
                            new DocumentLineWordCoordinates(),
                            null)
                    ]
                ),
                new DocumentLineColumn(
                    [
                        new DocumentLineWord(
                            "C",
                            100,
                            new DocumentLineWordCoordinates(),
                            null)
                    ]
                )
            ],
            0,
            0,
            0,
            0);
    }
}