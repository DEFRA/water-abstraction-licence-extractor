using System.Text.Json.Serialization;

namespace WRADI.Core.AbstractionLicence.Models;

public sealed class LicenceRunComparison
{
    public required string LicenceNumber { get; set; }

    public int NewerRunId { get; set; }

    public int OlderRunId { get; set; }

    public bool NewerRunHasData { get; set; }

    public bool OlderRunHasData { get; set; }

    public bool Identical { get; set; }

    public List<LicenceRunDifference> Differences { get; set; } = [];

    public List<LicenceRunDiffLine> Lines { get; set; } = [];
}

public enum LicenceRunDiffLineKind
{
    Context,
    Removed,
    Added,
    Skipped
}

public sealed class LicenceRunDiffLine
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LicenceRunDiffLineKind Kind { get; set; }

    public int? OlderLineNumber { get; set; }

    public int? NewerLineNumber { get; set; }

    public required string Text { get; set; }
}

public sealed class LicenceRunDifference
{
    public required string Path { get; set; }

    public string? Older { get; set; }

    public string? Newer { get; set; }
}
