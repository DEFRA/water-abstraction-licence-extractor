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

    public Stream GetFileAsStream(string filename)
    {
        return File.Open($"{FolderPath}{filename}", FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    public byte[] GetFileAsBytes(string pdfFilename)
    {
        return File.ReadAllBytes($"{FolderPath}{pdfFilename}");
    }

    public string FolderPath { get; set; } = folderPath;
}