using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Services.Services;

public class LocalFileService(string folderPath) : IFileService
{
    public string IngressFolderPath { get; set; } = folderPath;

    public string AssetsFolderPath { get; set; } = folderPath;
    
    public Task<List<string>> GetAllFilesAsync()
    {
        return Task.FromResult(
            Directory
                .GetFiles(IngressFolderPath)
                .Select(path => path.Split('/').Last())
                .ToList());
    }

    public Task<List<FileMetadata>> GetAllFilesWithMetadataAsync(string startAfter, int take)
    {
        var folder = new DirectoryInfo(IngressFolderPath);
        var filesInFolder = folder.GetFiles("*.*", SearchOption.AllDirectories);

        return Task.FromResult(filesInFolder
            .Select(f => new FileMetadata
            {
                Filename = f.Name,
                Filesize = f.Length,
                ModifiedTime = f.LastWriteTime
            })
            .ToList());
    }

    public Task<Stream?> GetFileAsStreamAsync(string filename)
    {
        var path = $"{IngressFolderPath}{filename}";
        
        return Task.FromResult<Stream?>(
            File.Open(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read));
    }

    public Task<byte[]> GetFileAsBytesAsync(string filename, int chunkIndex, int chunkSize)
    {
        return File.ReadAllBytesAsync($"{IngressFolderPath}{filename}");
    }

    public async Task UploadFileAsStreamAsync(string filename, Stream stream, string contentType, StorageFolder folder)
    {
        await Task.Delay(1000);
        var filePath = Path.Combine(IngressFolderPath, filename);
        await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        await stream.CopyToAsync(fileStream);
    }

    public async Task<string?> UploadFileChunkAsync(string filename, Stream stream, int chunkIndex, int totalChunks, string? uploadId = null)
    {
        await Task.Delay(1000);
        var filePath = Path.Combine(IngressFolderPath, filename);
        var mode = chunkIndex == 0 ? FileMode.Create : FileMode.Append;
        await using var fileStream = new FileStream(filePath, mode, FileAccess.Write, FileShare.None);
        await stream.CopyToAsync(fileStream);

        return null;
    }
    
    public Task DeleteAsync(string filename)
    {
        throw new NotImplementedException();
    }

    public Task<bool> ExistsAsync(string filename, StorageFolder folder)
    {
        throw new NotImplementedException();
    }

    public Task RenameAsync(string originalFilename, string newFilename)
    {
        throw new NotImplementedException();
    }

    public Task CopyAsync(string filename, string destinationBucketName)
    {
        throw new NotImplementedException();
    }
    
    public Task<string> GetPresignedUrlAsync(string filename, StorageFolder folder)
    {
        throw new NotImplementedException();
    }
}