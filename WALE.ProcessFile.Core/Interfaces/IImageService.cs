namespace WALE.ProcessFile.Core.Interfaces;

public interface IImageService
{
    Task UploadAsync(string key, Stream data, string contentType);

    Task<byte[]?> DownloadAsync(string key);

    Task<string> GetPresignedUrlAsync(string key);

    Task DeleteAsync(string key);
}
