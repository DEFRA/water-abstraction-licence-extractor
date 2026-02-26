using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Helpers;

namespace WALE.ProcessFile.Database.PostgreSQL.Helpers;

public static class ImageReferenceHelper
{
    public static List<(string ProviderName, string? ImageReference)> GetPageScreenshotReferences(int pageNumber, string pdfServiceName,
        string pdfFilePath)
    {
        var pdfFilename = FileHelper.GetFilenameWithoutExtension(pdfFilePath);

        return
        [
            (pdfServiceName,
                $"Screenshot-{pdfFilename}-{pdfServiceName}-{pageNumber}"),
            (GeneralConstants.DocnetExtractorServiceName,
                $"Screenshot-{pdfFilename}-{GeneralConstants.DocnetExtractorServiceName}-{pageNumber}")
        ];
    }
    
    public static string GetImageReference(
        int pageNumber,
        int imageNumber,
        string pdfFilePath,
        string extension)
    {
        var pdfFilename = FileHelper.GetFilenameWithoutExtension(pdfFilePath);
        return $"ImageReference-{pdfFilename}-{extension}-{pageNumber}-{imageNumber}";
    }
    
    public static string GetNoOcrPageReferenceAsync(string filepath, string noOcrServiceName, int pageNumber)
    {
        var pdfFilename = FileHelper.GetFilenameWithoutExtension(filepath);
        return $"NoOcrPageReference-{pdfFilename}-{noOcrServiceName}-{pageNumber}";
    }
}