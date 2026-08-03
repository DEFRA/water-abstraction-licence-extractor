using System.Text.Json;
using WRADI.Core.AbstractionLicence.Models;

namespace WRADI.Core.AbstractionLicence.Helpers;

public static class JsonHelper
{
    public static string GetAsString(Licence licence)
    {
        return JsonSerializer.Serialize(licence, WALE.ProcessFile.Core.Helpers.JsonHelper.GetSerializerOptions());
    }
    
    public static string GetAsString(Dictionary<string, LicenceSet> licenceSets)
    {
        return JsonSerializer.Serialize(licenceSets.Values, WALE.ProcessFile.Core.Helpers.JsonHelper.GetSerializerOptions());
    }
}