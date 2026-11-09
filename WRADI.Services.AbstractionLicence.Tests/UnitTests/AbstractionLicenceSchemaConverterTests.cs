using WRADI.DocumentType.AbstractionLicence.Converters;

namespace WRADI.Services.AbstractionLicence.Tests.UnitTests;

public class AbstractionLicenceSchemaConverterTests
{
    [Theory]
    [InlineData(0.42, 0.417, true)]
    [InlineData(0.417, 0.42, true)]
    [InlineData(41369.419, 41360, true)]
    [InlineData(41360, 41369.419, true)]
    [InlineData(42360, 41369.419, false)]
    [InlineData(1, 2, false)]
    [InlineData(1, 1.01, false)]
    [InlineData(1, 1.001, true)]
    [InlineData(1, 1.0075, true)]
    [InlineData(1, 1.0076, false)]
    public void SomeConversions(double value1, double value2, bool expected)
    {
        var result = AbstractionLicenceSchemaConverter.AreValuesEqual(value1, value2);
        Assert.Equal(expected, result);
    }
}