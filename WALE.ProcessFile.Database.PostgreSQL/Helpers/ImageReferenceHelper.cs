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
}