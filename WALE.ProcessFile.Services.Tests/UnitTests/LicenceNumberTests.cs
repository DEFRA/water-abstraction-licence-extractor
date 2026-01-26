using WALE.ProcessFile.Services.Formats;
using Xunit;

namespace WALE.ProcessFile.Services.Tests.UnitTests;

public class LicenceNumberTests
{
    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenDatabaseReadServiceIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new LicenceNumber(null!));
    }

    [Theory]
    [InlineData("12/34/56/78", "12345678")]
    [InlineData("AA/12/34/56", "AA123456")]
    [InlineData("01/23/45/67", "1234567")] // 0 is removed
    [InlineData("1/2/3.1/4", "12314")]
    [InlineData("1.2.3.4", "1234")]
    [InlineData("A-B_C/123", "ABC123")]
    public void NormalizeLicenceNumber_ShouldRemoveNonAlphanumericExceptZero(string input, string expected)
    {
        // Act
        var result = LicenceNumber.NormalizeLicenceNumber(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("12/34/56/78", new[] { "12", "34", "56", "78" })]
    [InlineData("AA/12/34/56", new[] { "AA", "12", "34", "56" })]
    [InlineData("01/002/003/00", new[] { "1", "2", "3", "" })]
    [InlineData("1/2/3.1/4", new[] { "1", "2", "31", "4" })] // Has / and . -> split on / only
    [InlineData("1.2.3.4", new[] { "1", "2", "3", "4" })] // Has only . -> split on .
    [InlineData("ABC-123.DEF", new[] { "ABC", "123DEF" })] // Has - and . -> split on - only
    [InlineData("A B.C", new[] { "A", "BC" })] // Has space and . -> split on space only
    public void ExtractSegments_ShouldSplitCorrectly(string input, string[] expected)
    {
        // Act
        var result = LicenceNumber.ExtractSegments(input);

        // Assert
        Assert.Equal(expected, result);
    }
    
    [Theory]
    [InlineData(new[] { "123", "456", "789", "101" }, new[] { "123004560789000101" }, true)]
    [InlineData(new[] { "1", "2", "3" }, new[] { "1", "2", "3" }, true)]
    [InlineData(new[] { "1", "2", "3" }, new[] { "1", "2" }, false)]
    [InlineData(new[] { "1", "2" }, new[] { "12" }, true)]
    [InlineData(new[] { "12" }, new[] { "1", "2" }, true)]
    [InlineData(new[] { "1", "2" }, new[] { "1", "3" }, false)]
    [InlineData(new[] { "123", "456" }, new[] { "1230456" }, true)]
    [InlineData(new[] { "123", "456" }, new[] { "1234560" }, false)]
    [InlineData(new[] { "123", "456" }, new[] { "12345","60" }, false)]
    [InlineData(new[] { "123", "456" }, new[] { "12","3456" }, true)]
    [InlineData(new[] { "123", "456" }, new[] { "12","300456" }, true)]
    [InlineData(new[] { "12003", "456" }, new[] { "12","300456" }, true)]
    public void SegmentsMatch_ShouldMatchCorrectly(string[] segments1, string[] segments2, bool expected)
    {
        // Act
        var result = LicenceNumber.SegmentsMatch(segments1.ToList(), segments2.ToList());

        // Assert
        Assert.Equal(expected, result);
    }
}
