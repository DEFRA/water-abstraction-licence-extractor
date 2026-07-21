namespace WALE.ProcessFile.Core.Helpers;

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

    public static Dictionary<string, string?> GetRelevantFilesInFolder(string folder)
    {
        return Directory
            .GetFiles(folder)
            .Where(fileName => fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            .Where(fileName => !fileName.Contains("WR179"))
            .Where(fileName => !fileName.Contains("Warning", StringComparison.OrdinalIgnoreCase))
            .Where(fileName => !fileName.Contains("Determination", StringComparison.OrdinalIgnoreCase))
            .Where(fileName => !fileName.Contains("Compliance", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(k => k, string? (_) => null);
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
    
    public static string? ExtractPermitNumber(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            return string.Empty;
        }

        var underscoreIndex = fileName.IndexOf("__", StringComparison.Ordinal);
        
        return underscoreIndex >= 0 
            ? fileName[..underscoreIndex].Trim() 
            : null;
    }
    
    public static Guid? ExtractFileId(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            return null;
        }

        var filenameParts = fileName.Split("__");
        var fileIdWithExtension = filenameParts.LastOrDefault()?.Trim();
        
        var fileIdString = fileIdWithExtension!.Split('.')[0];
        
        return Guid.TryParse(fileIdString, out var fileIdOut)
            ? fileIdOut
            : null;
    }
}