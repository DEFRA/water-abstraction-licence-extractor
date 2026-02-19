using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.PdfPig;

namespace WALE.ProcessFile.Services.PdfPig.Helpers;

public static class DocumentLineWordCoordinatesHelper
{
    public static DocumentLineWordCoordinates Convert(MinimalPdfRectangle rectangle)
    {
        return new DocumentLineWordCoordinates(
            rectangle.Top,
            rectangle.Right,
            rectangle.Bottom,
            rectangle.Left);
    }
}