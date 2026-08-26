using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Core.Interfaces;

public interface IFileService
{
    public Task<List<string>> GetAllFilesAsync();
    
    public Task<List<FileMetadata>> GetAllFilesWithMetadataAsync(string startAfter, int take);
    
    public Task<Stream?> GetFileAsStreamAsync(string filename);

    public Task<byte[]> GetFileAsBytesAsync(string filename, int chunkIndex, int chunkSize);
    
    public Task UploadFileAsStreamAsync(string filename, Stream stream);

    public Task<string?> UploadFileChunkAsync(string filename, Stream stream, int chunkIndex, int totalChunks, string? uploadId = null);
    
    public string FolderPath { get; set; }
    
    public Task DeleteAsync(string filename);
    
    Task<bool> ExistsAsync(string filename);
    
    public Task RenameAsync(string originalFilename, string newFilename);
    
    public Task<string> GetPresignedUrlAsync(string filename);
}