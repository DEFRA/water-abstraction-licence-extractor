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

    public Task<List<string>> GetFileNamesWithPrefixAsync(string prefix, StorageFolder folder)
    {
        var root = folder == StorageFolder.Ingress ? IngressFolderPath : AssetsFolderPath;

        if (!Directory.Exists(root))
        {
            return Task.FromResult(new List<string>());
        }

        return Task.FromResult(
            Directory
                .EnumerateFiles(root, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
                .Where(name => name.StartsWith(prefix, StringComparison.Ordinal))
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

    public async Task<byte[]> GetFileAsBytesAsync(string filename, int chunkIndex, int chunkSize)
    {
        await using var file = File.OpenRead($"{IngressFolderPath}{filename}");
        var start = (long)chunkIndex * chunkSize;

        if (start >= file.Length)
        {
            return [];
        }

        file.Position = start;

        var bytes = new byte[(int)Math.Min(chunkSize, file.Length - start)];
        await file.ReadExactlyAsync(bytes);

        return bytes;
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
    
    // No real presigned-URL concept for the local filesystem - point at FilesController's own
    // GetRawAsync action instead, which streams the file's bytes directly. Relative rather than
    // absolute (no base URL available here) - Redirect()/the browser both resolve a relative
    // Location against the current origin fine.
    public Task<string> GetPresignedUrlAsync(string filename, StorageFolder folder)
    {
        return Task.FromResult($"/BFF/Files/GetRaw?filename={Uri.EscapeDataString(filename)}");
    }

    // No durable address for a local file, so this stays the relative BFF path - callers that need
    // an absolute URL resolve it against their own request.
    public string GetHttpsUrl(string filename, StorageFolder folder)
    {
        return $"/BFF/Files/GetRaw?filename={Uri.EscapeDataString(filename)}";
    }
}