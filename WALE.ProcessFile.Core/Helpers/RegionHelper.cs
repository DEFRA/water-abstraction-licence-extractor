namespace WALE.ProcessFile.Core.Helpers;

public static class RegionHelper
{
    private static readonly IReadOnlyDictionary<int, string> RegionNamesById = new Dictionary<int, string>
    {
        [1] = "Anglian",
        [2] = "Midlands",
        [3] = "North East",
        [4] = "North West",
        [5] = "South West",
        [6] = "Southern",
        [7] = "Thames",
        [8] = "Wales",
    };

    private static readonly IReadOnlyDictionary<string, int> RegionIdsByName =
        RegionNamesById.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);

    public static string? GetRegionNameOrNull(int? regionCode)
    {
        return regionCode is { } code && RegionNamesById.TryGetValue(code, out var name)
            ? name
            : null;
    }

    public static string GetRegionName(int regionCode)
    {
        return RegionNamesById.TryGetValue(regionCode, out var name)
            ? name
            : throw new ArgumentOutOfRangeException(nameof(regionCode), $"We've not yet mapped region code {regionCode}");
    }

    public static int GetRegionId(string regionName)
    {
        return RegionIdsByName.TryGetValue(regionName, out var id)
            ? id
            : throw new ArgumentOutOfRangeException(nameof(regionName), $"We've not yet mapped region name {regionName}");
    }
}
