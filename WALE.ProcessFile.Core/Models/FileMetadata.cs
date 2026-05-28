namespace WALE.ProcessFile.Core.Models;

public record FileMetadata
{
    public required string Filename { get; init; }
    
    public required long Filesize { get; init; }
    
    public required DateTime ModifiedTime { get; init; }
}