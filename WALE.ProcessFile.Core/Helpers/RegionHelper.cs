using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OutputSchema;

namespace WALE.ProcessFile.Core.Helpers;

public static class RegionHelper
{
    public static string GetRegionName(int regionCode)
    {
        return regionCode switch
        {
            1 => "Anglian",
            2 => "Midlands",
            3 => "North East",
            4 => "North West",
            5 => "South West",
            6 => "Southern",
            7 => "Thames",
            8 => "Wales",
            _ => throw new ArgumentOutOfRangeException(nameof(regionCode), $"We've not yet mapped region code {regionCode}")
        };
    }
    
    public static int GetRegionId(string regionName)
    {
        return regionName switch
        {
            "Anglian" => 1,
            "Midlands" => 2,
            "North East" => 3,
            "North West" => 4,
            "South West" => 5,
            "Southern" => 6,
            "Thames" => 7,
            "Wales" => 8,
            _ => throw new ArgumentOutOfRangeException(nameof(regionName), $"We've not yet mapped region name {regionName}")
        };
    }
}