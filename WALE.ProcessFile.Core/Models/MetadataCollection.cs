using WALE.ProcessFile.Core.Models.OutputSchema;

namespace WALE.ProcessFile.Core.Models;

public class MetadataCollection
{
    public Dictionary<string, object>? PagesMetadata { get; set; }
    public Dictionary<int, string>? AllDocumentLines { get; set; }
    public ImageMetadata? ImageMetadata { get; set; }
}