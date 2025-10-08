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
        
        return string.Join(compositeCharacter, filenameParts.Take(filenameParts.Length - 1));
    }
}