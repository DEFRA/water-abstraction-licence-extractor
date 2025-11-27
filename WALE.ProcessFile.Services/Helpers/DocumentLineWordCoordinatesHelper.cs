using UglyToad.PdfPig.Core;
using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Services.Helpers;

public static class DocumentLineWordCoordinatesHelper
{
    public static DocumentLineWordCoordinates Convert(PdfRectangle rectangle)
    {
        return new DocumentLineWordCoordinates(
            rectangle.Top,
            rectangle.Right,
            rectangle.Bottom,
            rectangle.Left);
    }
}