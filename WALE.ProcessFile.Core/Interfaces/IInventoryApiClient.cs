using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Core.Interfaces;

public interface IInventoryApiClient
{
    Task<List<FileMetadata>> GetAllWithMetadataAsync(string startAfter, int take);
}