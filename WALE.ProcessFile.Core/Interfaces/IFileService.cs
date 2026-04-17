namespace WALE.ProcessFile.Core.Interfaces;

public interface IFileService
{
    public Task<List<string>> GetAllFilesAsync();
    
    public Task<Stream> GetFileAsStreamAsync(string filename);

    public Task<byte[]> GetFileAsBytesAsync(string pdfFilename);
    
    public Task UploadFileAsStreamAsync(string pdfFilename, Stream stream);
    
    public string FolderPath { get; set; }
}