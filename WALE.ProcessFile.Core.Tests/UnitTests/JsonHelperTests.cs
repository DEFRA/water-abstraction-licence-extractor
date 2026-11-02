using System.Text.Json;
using WALE.ProcessFile.Core.Helpers;
using Xunit;

namespace WALE.ProcessFile.Core.Tests.UnitTests;

public class JsonHelperTests
{
    private enum Shape { RoundedCorner }

    private sealed class Payload
    {
        public Shape Shape { get; init; }
        public string? Present { get; init; }
        public string? Absent { get; init; }
    }

    /// The wire options are the standard ones with indentation turned off, so anything added to
    /// GetSerializerOptions has to reach them too.
    [Fact]
    public void WireOptionsDifferFromTheStandardOnesOnlyByIndentation()
    {
        var payload = new Payload { Shape = Shape.RoundedCorner, Present = "x" };

        var standard = JsonSerializer.Serialize(payload, JsonHelper.GetSerializerOptions());
        var wire = JsonSerializer.Serialize(payload, JsonHelper.GetWireSerializerOptions());

        Assert.Contains("\n", standard);
        Assert.DoesNotContain("\n", wire);

        // camelCase names, camelCase enum values and null omission all survive the copy
        Assert.Equal("""{"shape":"roundedCorner","present":"x"}""", wire);
        Assert.Equal(
            standard.Replace(" ", "").Replace("\n", "").Replace("\r", ""),
            wire.Replace(" ", ""));
    }
}
