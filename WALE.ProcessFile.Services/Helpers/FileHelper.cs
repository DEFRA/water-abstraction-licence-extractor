namespace WALE.ProcessFile.Services.Helpers;

public class FileHelper
{
    public static string GetFilenameWithoutExtensions(string pdfFilePath)
    {
        var filenameParts = pdfFilePath.Split('/').Last().Split('.');
        return string.Join('-', filenameParts.Take(filenameParts.Length - 1));
    }
}