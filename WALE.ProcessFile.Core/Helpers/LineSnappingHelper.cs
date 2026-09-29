namespace WALE.ProcessFile.Core.Helpers;

public static class LineSnappingHelper
{
    public static int RoundToNearestN(double bottomPosition, double roundTo, string text)
    {
        bottomPosition = CompensateForBelowTheLineCharactersOffset(text, bottomPosition, roundTo);
        
        var remainder = bottomPosition % roundTo;
        bottomPosition += (remainder <= roundTo / 2) ? -remainder : (roundTo - remainder);
            
        return (int)bottomPosition;
    }

    public static double CompensateForBelowTheLineCharactersOffset(string text, double bottomY, double roundTo)
    {
        const double ratio = 1 / 9.0; // We used to deduct a static '1' but we wan't this to change based on lineheight
        var deductionAmount = roundTo * ratio * -1.0;
        
        var belowTheLineCharacters = new List<char>
        {
            'p',
            'q',
            'y'
        };

        return bottomY + (belowTheLineCharacters.Any(text.Contains) ? deductionAmount : 0);
    }
}