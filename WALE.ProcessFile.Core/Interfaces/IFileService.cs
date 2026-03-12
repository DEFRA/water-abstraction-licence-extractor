namespace WALE.ProcessFile.Core.Interfaces;

public interface IFileService
{
    public List<string> GetAllFiles();
    
    public Stream GetFileAsStream(string filename);

    public byte[] GetFileAsBytes(string pdfFilename);
    
    public string FolderPath { get; set; }
}