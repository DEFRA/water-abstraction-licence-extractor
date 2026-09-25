using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Core.Interfaces;

public interface IFileService
{
    public string IngressFolderPath { get; set; }
    
    public string AssetsFolderPath { get; set; }
    
    public Task<List<string>> GetAllFilesAsync();
    
    public Task<List<FileMetadata>> GetAllFilesWithMetadataAsync(string startAfter, int take);
    
    public Task<Stream?> GetFileAsStreamAsync(string filename);

    public Task<byte[]> GetFileAsBytesAsync(string filename, int chunkIndex, int chunkSize);
    
    public Task UploadFileAsStreamAsync(string filename, Stream stream, string contentType, StorageFolder folder);

    public Task<string?> UploadFileChunkAsync(string filename, Stream stream, int chunkIndex, int totalChunks, string? uploadId = null);
    
    public Task DeleteAsync(string filename);
    
    Task<bool> ExistsAsync(string filename, StorageFolder folder);
    
    public Task RenameAsync(string originalFilename, string newFilename);
    
    public Task CopyAsync(string filename, string destinationBucketName);
    
    public Task<string> GetPresignedUrlAsync(string filename, StorageFolder folder);
}