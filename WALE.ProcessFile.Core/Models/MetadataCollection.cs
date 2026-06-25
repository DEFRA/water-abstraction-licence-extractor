using WALE.ProcessFile.Core.Models.OutputSchema;

namespace WALE.ProcessFile.Core.Models;

public class MetadataCollection
{
    public Dictionary<string, object>? PagesMetadata { get; init; }
    public Dictionary<int, string>? AllDocumentLines { get; init; }
    public ImageMetadata? ImageMetadata { get; init; }
    public long SizeBytes { get; set; }
}