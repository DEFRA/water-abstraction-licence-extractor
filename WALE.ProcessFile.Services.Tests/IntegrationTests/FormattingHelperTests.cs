using Meziantou.Xunit;
using WALE.ProcessFile.Core.Helpers;

namespace WALE.ProcessFile.Services.Tests.IntegrationTests;

[EnableParallelization]
public class FormattingHelperTests
{
    [Fact]
    public void WhenX_Y_ThenX()
    {
        var actual = FormattingHelper.FormatLicenceNumber("1/22/03/131/1", 3);
        Assert.Equal("1/22/03/131/1", actual);
    }
    
    [Fact]
    public void WhenX_Y_ThenX2()
    {
        var actual = FormattingHelper.FormatLicenceNumber("12100073R01", 3);
        Assert.Equal("1/21/00/073/R01", actual);
    }
    
    [Fact]
    public void WhenX_Y_ThenX3()
    {
        var actual = FormattingHelper.FormatLicenceNumber("22632295A", 3);
        Assert.Equal("2/26/32/295A", actual);
    }
    
    [Fact]
    public void WhenX_Y_ThenX4()
    {
        var actual = FormattingHelper.FormatLicenceNumber("NE0220000001R01", 3);
        Assert.Equal("NE/022/0000/001/R01", actual);
    }
    
    [Fact]
    public void WhenX_Y_ThenX5()
    {
        var actual = FormattingHelper.FormatLicenceNumber("NE0240005014", 3);
        Assert.Equal("NE/024/0005/014", actual);
    }
    
    [Fact]
    public void WhenX_Y_ThenX6()
    {
        var actual = FormattingHelper.FormatLicenceNumber("22708119", 3);
        Assert.Equal("2/27/08/119", actual);
    }
    
    [Fact]
    public void WhenX_Y_ThenX7()
    {
        var actual = FormattingHelper.FormatLicenceNumber("2/27/09/25", 3);
        Assert.Equal("2/27/09/025", actual);
    }
    
    [Fact]
    public void WhenX_Y_ThenX8()
    {
        var actual = FormattingHelper.FormatLicenceNumber("1/22/2/87", 3);
        Assert.Equal("1/22/02/087", actual);
    }
}