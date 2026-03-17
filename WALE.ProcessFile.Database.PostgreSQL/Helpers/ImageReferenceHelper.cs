using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Helpers;

namespace WALE.ProcessFile.Database.PostgreSQL.Helpers;

public static class ImageReferenceHelper
{
    public static List<(string ProviderName, string? ImageReference)> GetPageScreenshotReferences(
        int pageNumber,
        string pdfServiceName,
        string filename)
    {
        var filenameNoExtension = FileHelper.GetFilenameWithoutExtension(filename);

        return
        [
            (pdfServiceName,
                $"Screenshot-{filenameNoExtension}-{pdfServiceName}-{pageNumber}"),
            (GeneralConstants.DocnetExtractorServiceName,
                $"Screenshot-{filenameNoExtension}-{GeneralConstants.DocnetExtractorServiceName}-{pageNumber}")
        ];
    }
    
    public static string GetImageReference(
        int pageNumber,
        int imageNumber,
        string filename,
        string extension)
    {
        var filenameNoExtension = FileHelper.GetFilenameWithoutExtension(filename);
        return $"ImageReference-{filenameNoExtension}-{extension}-{pageNumber}-{imageNumber}";
    }
    
    public static string GetNoOcrPageReferenceAsync(
        string filename,
        string noOcrServiceName,
        int pageNumber)
    {
        var filenameNoExtension = FileHelper.GetFilenameWithoutExtension(filename);
        return $"NoOcrPageReference-{filenameNoExtension}-{noOcrServiceName}-{pageNumber}";
    }
}