using System.Text.Json;
using System.Text.Json.Nodes;
using WRADI.Core.AbstractionLicence.Interfaces;
using WRADI.Core.AbstractionLicence.Models;

namespace WRADI.Services.Output.AbstractionLicence;

public class LicenceRunComparisonService : ILicenceRunComparisonService
{
    // Bookkeeping, not extraction: between runs 168 and 169 of 01/115 these four were the only
    // difference in 7.8kB of JSON, so a comparison that keeps them reports noise on every pair.
    private static readonly string[] IdentityFields =
    [
        "id",
        "licenceId",
        "matchesResultId",
        "processRunId"
    ];

    private const int MaximumDifferences = 500;

    public LicenceRunComparison Compare(
        string licenceNumber,
        int newerRunId,
        string? newerData,
        int olderRunId,
        string? olderData)
    {
        var comparison = new LicenceRunComparison
        {
            LicenceNumber = licenceNumber,
            NewerRunId = newerRunId,
            OlderRunId = olderRunId,
            NewerRunHasData = newerData is not null,
            OlderRunHasData = olderData is not null
        };

        if (newerData is null || olderData is null)
        {
            return comparison;
        }

        var newer = Normalise(newerData);
        var older = Normalise(olderData);

        Walk(string.Empty, older, newer, comparison.Differences);

        comparison.Identical = comparison.Differences.Count == 0;

        if (!comparison.Identical)
        {
            comparison.Lines = UnifiedDiff.Build(Render(older), Render(newer));
        }

        return comparison;
    }

    /// Key order is the serialiser's and matches between runs, so the diff is not sorted -
    /// sorting would hide a genuine reordering behind a tidier read.
    private static string[] Render(JsonNode? node) =>
        (node?.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) ?? string.Empty)
            .Replace("\r\n", "\n")
            .Split('\n');

    private static JsonNode? Normalise(string json)
    {
        var node = JsonNode.Parse(json);

        if (node is JsonObject root)
        {
            foreach (var field in IdentityFields)
            {
                root.Remove(field);
            }
        }

        return node;
    }

    private static void Walk(
        string path,
        JsonNode? older,
        JsonNode? newer,
        List<LicenceRunDifference> differences)
    {
        if (differences.Count >= MaximumDifferences)
        {
            return;
        }

        switch (older, newer)
        {
            case (JsonObject olderObject, JsonObject newerObject):
                foreach (var name in olderObject
                             .Select(property => property.Key)
                             .Union(newerObject.Select(property => property.Key)))
                {
                    Walk(
                        $"{path}/{name}",
                        olderObject[name],
                        newerObject[name],
                        differences);
                }

                return;

            case (JsonArray olderArray, JsonArray newerArray):
                if (olderArray.Count != newerArray.Count)
                {
                    Add(path, $"{olderArray.Count} items", $"{newerArray.Count} items", differences);

                    return;
                }

                for (var index = 0; index < olderArray.Count; index++)
                {
                    Walk($"{path}[{index}]", olderArray[index], newerArray[index], differences);
                }

                return;

            default:
                var olderText = Describe(older);
                var newerText = Describe(newer);

                if (olderText != newerText)
                {
                    Add(path, olderText, newerText, differences);
                }

                return;
        }
    }

    private static void Add(
        string path,
        string? older,
        string? newer,
        List<LicenceRunDifference> differences) =>
        differences.Add(
            new LicenceRunDifference
            {
                Path = path.Length == 0 ? "/" : path,
                Older = older,
                Newer = newer
            });

    private static string? Describe(JsonNode? node) =>
        node is null
            ? null
            : node.GetValueKind() == JsonValueKind.String
                ? node.GetValue<string>()
                : node.ToJsonString();
}
