namespace WALE.ProcessFile.Services.Helpers;

public static class FileHelper
{
    public static string? GetFilenameWithExtension(string? pdfFilePath)
    {
        const char pathSeparator = '/';

        return pdfFilePath?
            .Split(pathSeparator)
            .Last()
            .Trim();
    }
    
    public static string? GetFilenameWithoutExtension(string? pdfFilePath)
    {
        const char extensionSeperator = '.';
        const char compositeCharacter = '-';

        var filenameWithExtensions = GetFilenameWithExtension(pdfFilePath);

        if (string.IsNullOrEmpty(filenameWithExtensions))
        {
            return filenameWithExtensions;
        }
        
        var filenameParts = filenameWithExtensions.Split(extensionSeperator);

        if (filenameParts.Length == 1)
        {
            return filenameParts[0].Trim();
        }
        
        var returnString  = string.Join(compositeCharacter, filenameParts.Take(filenameParts.Length - 1));
        return returnString.Trim();
    }
}