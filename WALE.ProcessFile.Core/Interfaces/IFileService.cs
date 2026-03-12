namespace WALE.ProcessFile.Core.Interfaces;

public interface IFileService
{
    public List<string> GetAllFiles();
    
    public string FolderPath { get; set; }
}