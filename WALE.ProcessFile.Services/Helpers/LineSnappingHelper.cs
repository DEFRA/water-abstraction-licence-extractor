namespace WALE.ProcessFile.Services.Helpers;

public static class LineSnappingHelper
{
    public static int SnapToPageRow(
        double textPosition,
        double lineHeight,
        double firstTextPosition)
    {
        const double defaultTopPosition = 1000.0;
        const double splitIntoHalf = 2.0;
            
        var currentRowPosition = firstTextPosition; // e.g. 231.4
        var halfLineHeight = lineHeight / splitIntoHalf; // e.g. 5.5

        do
        {
            var rowTop = currentRowPosition + halfLineHeight; // e.g. 236.4
            var rowBottom = currentRowPosition - halfLineHeight; // e.g. 225.4

            // A lower top value is further down the page
            if (textPosition <= rowTop && textPosition >= rowBottom)
            {
                return (int)currentRowPosition;
            }

            currentRowPosition -= lineHeight;
        } while (currentRowPosition - lineHeight >= -lineHeight);

        const double negligibleTolerance = 0.00001;
            
        if (Math.Abs(firstTextPosition - defaultTopPosition) < negligibleTolerance)
        {
            throw new ArgumentOutOfRangeException(nameof(firstTextPosition));
        }
            
        return SnapToPageRow(textPosition, lineHeight, defaultTopPosition);
    }
        
    public static int RoundToNearestN(double value, double roundTo)
    {
        var remainder = value % roundTo;
        value += (remainder <= roundTo / 2) ? -remainder : (roundTo - remainder);
            
        return (int)value;
    }
}