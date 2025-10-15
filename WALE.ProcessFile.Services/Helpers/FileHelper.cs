namespace WALE.ProcessFile.Services.Helpers;

public static class FileHelper
{
    public static string GetFilenameWithoutExtension(string pdfFilePath)
    {
        const char pathSeparator = '/';
        const char extensionSeperator = '.';
        const char compositeCharacter = '-';
        
        var filenameParts = pdfFilePath
            .Split(pathSeparator)
            .Last()
            .Split(extensionSeperator);

        if (filenameParts.Length == 1)
        {
            return filenameParts[0].Trim();
        }
        
        var returnString  = string.Join(compositeCharacter, filenameParts.Take(filenameParts.Length - 1));
        return returnString.Trim();
    }
}