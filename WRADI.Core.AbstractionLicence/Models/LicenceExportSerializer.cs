using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace WRADI.Core.AbstractionLicence.Models;

public static class LicenceExportSerializer
{
    private const string InheritedPlumbing = "noneSchemaData";

    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers = { DropInheritedPlumbing }
        }
    };

    /// `new` plus [JsonIgnore] on LicenceExport does not suppress it - System.Text.Json
    /// serialises the base property regardless, so it has to come out of the type info.
    private static void DropInheritedPlumbing(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Type != typeof(LicenceExport))
        {
            return;
        }

        for (var index = typeInfo.Properties.Count - 1; index >= 0; index--)
        {
            if (typeInfo.Properties[index].Name == InheritedPlumbing)
            {
                typeInfo.Properties.RemoveAt(index);
            }
        }
    }
}
