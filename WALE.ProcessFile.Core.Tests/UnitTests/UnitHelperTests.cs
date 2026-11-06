using WALE.ProcessFile.Core.Helpers;

namespace WALE.ProcessFile.Core.Tests.UnitTests;

public class UnitHelperTests
{
    [Theory]
    // Pass throughs
    [InlineData("litres", 1, "litres", 1)]
    [InlineData("gallons", 1, "gallons", 1)]
    [InlineData("megagallons", 1, "megagallons", 1)]
    [InlineData("million gallons", 1, "million gallons", 1)]
    [InlineData("million gallons", 1, "megagallons", 1)]
    [InlineData("cubic meters", 1, "cubic meters", 1)]
    [InlineData("thousand cubic metres", 1, "thousand cubic metres", 1)]
    // Conversions
    [InlineData("gallons", 1, "litres", 4.54609)]
    [InlineData("gallons", 1_000_000, "megagallons", 1)]
    [InlineData("gallons", 1_000_000, "million gallons", 1)]
    [InlineData("gallons", 1_000_000, "cubic meters", 4546.0900000000001)]
    [InlineData("gallons", 1_000_000, "thousand cubic meters", 4.5460900000000004)]
    [InlineData("megagallons", 1, "litres", 4_546_090)]
    [InlineData("megagallons", 1_000_000, "gallons", 1_000_000_000_000)]
    [InlineData("megagallons", 1_000_000, "million gallons", 1_000_000)]
    [InlineData("megagallons", 1_000_000, "cubic meters", 4_546_090_000)]
    [InlineData("megagallons", 1_000_000, "thousand cubic meters", 4_546_090)]    
    [InlineData("million gallons", 1, "litres", 4_546_090)]
    [InlineData("million gallons", 1_000_000, "gallons", 1_000_000_000_000)]
    [InlineData("million gallons", 1_000_000, "megagallons", 1_000_000)]
    [InlineData("million gallons", 1_000_000, "cubic meters", 4_546_090_000)]
    [InlineData("million gallons", 1_000_000, "thousand cubic meters", 4_546_090)]    
    [InlineData("litres", 1, "megalitres", 1e-6)]
    [InlineData("litres", 1, "gallons", 0.21997)]
    [InlineData("litres", 1_000_000, "megagallons", 0.21997)]
    [InlineData("litres", 1_000_000, "million gallons", 0.21997)]
    [InlineData("litres", 1_000_000, "cubic meters", 1_000)]
    [InlineData("litres", 1_000_000, "thousand cubic meters", 1)]
    [InlineData("megalitres", 1, "litres", 1_000_000)]
    [InlineData("megalitres", 1, "gallons", 219_970)]
    [InlineData("megalitres", 1_000_000, "megagallons", 219_970)]
    [InlineData("megalitres", 1_000_000, "million gallons", 219_970)]
    [InlineData("megalitres", 1_000_000, "cubic meters", 1_000_000_000)]
    [InlineData("megalitres", 1_000_000, "thousand cubic meters", 1_000_000)]
    [InlineData("cubic metres", 1, "litres", 1_000)]
    [InlineData("cubic metres", 1, "gallons", 219.96899999999999)]
    [InlineData("cubic metres", 1_000_000, "megagallons",  219.96899999999999)]
    [InlineData("cubic metres", 1_000_000, "million gallons",  219.96899999999999)]
    [InlineData("cubic metres", 1_000_000, "thousand cubic meters", 1_000)]
    [InlineData("thousand cubic metres", 1, "litres", 1_000_000)]
    [InlineData("thousand cubic metres", 1, "gallons", 219_969)]
    [InlineData("thousand cubic metres", 1_000_000, "megagallons",  219_969)]
    [InlineData("thousand cubic metres", 1_000_000, "million gallons",  219_969)]
    [InlineData("thousand cubic metres", 1_000_000, "thousand cubic meters", 1_000_000)]
    // Misspellings
    [InlineData("liters", 1_000_000, "megagallons", 0.21997)]
    [InlineData("cubic meters", 1_000_000, "megagallons",  219.96899999999999)]
    [InlineData("thousand cubic meters", 1, "thousand cubic meters", 1)]
    public void SomeConversions(string sourceUnits, double sourceValue, string destinationUnits, double expected)
    {
        var result = UnitHelper.Convert(sourceValue, sourceUnits, destinationUnits);
        Assert.Equal(expected, result);
    }
}