using WALE.ProcessFile.Core.Helpers;

namespace WALE.ProcessFile.Services.Tests.IntegrationTests;

public class FormattingHelperTests
{
    [Fact]
    public void WhenX_Y_ThenX()
    {
        var actual = FormattingHelper.FormatLicenceNumber("1/22/03/131/1");
        Assert.Equal("1/22/03/131/1", actual);
    }
    
    [Fact]
    public void WhenX_Y_ThenX2()
    {
        var actual = FormattingHelper.FormatLicenceNumber("12100073R01");
        Assert.Equal("1/21/00/073/R01", actual);
    }
    
    [Fact]
    public void WhenX_Y_ThenX3()
    {
        var actual = FormattingHelper.FormatLicenceNumber("22632295A");
        Assert.Equal("2/26/32/295/A", actual);
    }
    
    [Fact]
    public void WhenX_Y_ThenX4()
    {
        var actual = FormattingHelper.FormatLicenceNumber("NE0220000001R01");
        Assert.Equal("NE/022/0000/001/R01", actual);
    }
    
    [Fact]
    public void WhenX_Y_ThenX5()
    {
        var actual = FormattingHelper.FormatLicenceNumber("NE0240005014");
        Assert.Equal("NE/024/0005/014", actual);
    }
    
    [Fact]
    public void WhenX_Y_ThenX6()
    {
        var actual = FormattingHelper.FormatLicenceNumber("22708119");
        Assert.Equal("2/27/08/119", actual);
    }
    
    [Fact]
    public void WhenX_Y_ThenX7()
    {
        var actual = FormattingHelper.FormatLicenceNumber("2/27/09/25");
        Assert.Equal("2/27/09/025", actual);
    }
}