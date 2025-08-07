namespace WALE.ProcessFile.Services.Helpers;

public static class LineSnappingHelper
{
    public static int RoundToNearestN(double bottomPosition, double roundTo, string text)
    {
        bottomPosition = CompensateForBelowTheLineCharactersOffset(text, bottomPosition);
        
        var remainder = bottomPosition % roundTo;
        bottomPosition += (remainder <= roundTo / 2) ? -remainder : (roundTo - remainder);
            
        return (int)bottomPosition;
    }

    public static double CompensateForBelowTheLineCharactersOffset(string text, double bottomY)
    {
        var belowTheLineCharacters = new List<char>
        {
            'p',
            'q',
            'y'
        };

        return bottomY + (belowTheLineCharacters.Any(text.Contains) ? -1 : 0);
    }
}