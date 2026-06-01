using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Services.Services;

public class LocalFileService(string folderPath) : IFileService
{
    public Task<List<string>> GetAllFilesAsync()
    {
        return Task.FromResult(
            Directory
                .GetFiles(FolderPath)
                .Select(path => path.Split('/').Last())
                .ToList());
    }

    public Task<List<FileMetadata>> GetAllFilesWithMetadataAsync(string startAfter, int take)
    {
        var folder = new DirectoryInfo(FolderPath);
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

    public Task<Stream> GetFileAsStreamAsync(string filename)
    {
        return Task.FromResult<Stream>(
            File.Open(
                $"{FolderPath}{filename}",
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read));
    }

    public Task<byte[]> GetFileAsBytesAsync(string filename)
    {
        return File.ReadAllBytesAsync($"{FolderPath}{filename}");
    }

    public async Task UploadFileAsStreamAsync(string filename, Stream stream)
    {
        await Task.Delay(1000);
        var filePath = Path.Combine(FolderPath, filename);
        await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        await stream.CopyToAsync(fileStream);
    }

    public async Task<string?> UploadFileChunkAsync(string filename, Stream stream, int chunkIndex, int totalChunks, string? uploadId = null)
    {
        await Task.Delay(1000);
        var filePath = Path.Combine(FolderPath, filename);
        var mode = chunkIndex == 0 ? FileMode.Create : FileMode.Append;
        await using var fileStream = new FileStream(filePath, mode, FileAccess.Write, FileShare.None);
        await stream.CopyToAsync(fileStream);

        return null;
    }

    public string FolderPath { get; set; } = folderPath;
    public Task DeleteAsync(string filename)
    {
        throw new NotImplementedException();
    }

    public Task<bool> ExistsAsync(string filename)
    {
        throw new NotImplementedException();
    }
}