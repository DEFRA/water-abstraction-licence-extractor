using WALE.ProcessFile.Core.Interfaces;

namespace WALE.ProcessFile.Services.Services;

public class LocalFileService(string folderPath) : IFileService
{
    public List<string> GetAllFiles()
    {
        return Directory
            .GetFiles(FolderPath)
            .Select(path => path.Split('/').Last())
            .ToList();
    }

    public string FolderPath { get; set; } = folderPath;
}