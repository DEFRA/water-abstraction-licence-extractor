using WALE.ProcessFile.Core.Interfaces;

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

    public Task UploadFileAsStreamAsync(string filename, Stream stream)
    {
        throw new NotImplementedException();
    }

    public string FolderPath { get; set; } = folderPath;
}