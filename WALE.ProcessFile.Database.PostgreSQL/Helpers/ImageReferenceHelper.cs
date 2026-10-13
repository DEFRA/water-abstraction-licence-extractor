using WALE.ProcessFile.Core.Constants;

namespace WALE.ProcessFile.Database.PostgreSQL.Helpers;

public static class ImageReferenceHelper
{
    public static List<(string ProviderName, string? ImageReference)> GetPageScreenshotReferences(
        int pageNumber,
        string pdfServiceName,
        Guid fileId)
    {
        return
        [
            (pdfServiceName,
                $"Screenshot-{fileId}-{pdfServiceName}-{pageNumber}"),
            (GeneralConstants.DocnetExtractorServiceName,
                $"Screenshot-{fileId}-{GeneralConstants.DocnetExtractorServiceName}-{pageNumber}")
        ];
    }
    
    public static string GetImageReference(
        int pageNumber,
        int imageNumber,
        Guid fileId,
        string extension)
    {
        return $"ImageReference-{fileId}-{extension}-{pageNumber}-{imageNumber}";
    }
    
    public static string GetNoOcrPageReferenceAsync(
        Guid fileId,
        string noOcrServiceName,
        int pageNumber)
    {
        return $"NoOcrPageReference-{fileId}-{noOcrServiceName}-{pageNumber}";
    }

    public static string GetPageScreenshotS3Key(
        Guid fileId,
        string noOcrServiceName,
        int pageNumber)
    {
        return $"page-screenshot/{fileId}/{noOcrServiceName}/{pageNumber}.jpg";
    }

    public static string GetPageScreenshotThumbnailS3Key(
        Guid fileId,
        string noOcrServiceName,
        int pageNumber)
    {
        return $"page-screenshot-thumbnail/{fileId}/{noOcrServiceName}/{pageNumber}.jpg";
    }

    public static string GetImageOnPageS3Key(
        Guid fileId,
        string noOcrServiceName,
        int pageNumber,
        int imageNumber,
        string extension)
    {
        return $"image-on-page/{fileId}/{noOcrServiceName}/{pageNumber}/{imageNumber}.{extension}";
    }
}