namespace WALE.ProcessFile.Services.Helpers;

public static class FileHelper
{
    public static string GetFilenameWithExtension(string pdfFilePath)
    {
        const char pathSeparator = '/';

        return pdfFilePath
            .Split(pathSeparator)
            .Last()
            .Trim();
    }
    
    public static string GetFilenameWithoutExtension(string pdfFilePath)
    {
        const char extensionSeperator = '.';
        const char compositeCharacter = '-';
        
        var filenameParts = GetFilenameWithExtension(pdfFilePath)
            .Split(extensionSeperator);

        if (filenameParts.Length == 1)
        {
            return filenameParts[0].Trim();
        }
        
        var returnString  = string.Join(compositeCharacter, filenameParts.Take(filenameParts.Length - 1));
        return returnString.Trim();
    }

    public static string GetImageExtension(string imageReference)
    {
        var extension = "bmp";
            
        if (imageReference.Contains("png"))
        {
            extension = "png";
        }
        else if (imageReference.Contains("jpg"))
        {
            extension = "jpg";
        }

        return extension;
    }
}