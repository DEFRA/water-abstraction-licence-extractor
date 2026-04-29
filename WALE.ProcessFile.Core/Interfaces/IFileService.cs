namespace WALE.ProcessFile.Core.Interfaces;

public interface IFileService
{
    public Task<List<string>> GetAllFilesAsync();
    
    public Task<Stream> GetFileAsStreamAsync(string filename);

    public Task<byte[]> GetFileAsBytesAsync(string filename);
    
    public Task UploadFileAsStreamAsync(string filename, Stream stream);
    
    public string FolderPath { get; set; }
}