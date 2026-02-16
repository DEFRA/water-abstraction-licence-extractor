namespace WALE.ProcessFile.Services.Helpers;

public static class SharedHelper
{
    public static string? ExtractPermitNumberFromFilename(string filename)
    {
        if (string.IsNullOrEmpty(filename))
        {
            return null;
        }

        // Remove file extension first
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(filename);

        // Find first underscore and extract everything before it
        var underscoreIndex = nameWithoutExtension.IndexOf('_');

        if (underscoreIndex > 0)
        {
            return nameWithoutExtension[..underscoreIndex].Replace(" ", string.Empty);
        }

        // If no underscore found, return the whole filename without extension
        return nameWithoutExtension.Replace(" ", string.Empty);
    }
}