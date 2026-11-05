namespace WALE.ProcessFile.Core.Helpers;

public static class UnitHelper
{
    public static double Convert(
        double sourceValue,
        string sourceUnits,
        string destinationUnits)
    {
        const double gallonsToMegaGallons = 1_000_000;

        sourceUnits = sourceUnits.Replace(" ", string.Empty).ToLower();
        destinationUnits = destinationUnits.Replace(" ", string.Empty).ToLower();

        return destinationUnits switch
        {
            "gallons" => ConvertToGallons(sourceValue, sourceUnits),
            "megagallons" => ConvertToGallons(sourceValue, sourceUnits) * gallonsToMegaGallons,
            "litres" => ConvertToLitres(sourceValue, sourceUnits),
            _ => throw new Exception($"Unit conversion not mapped - {sourceUnits} to {destinationUnits}")
        };
    }

    private static double ConvertToLitres(
        double sourceValue,
        string sourceUnits)
    {
        const double gallonsToLitres = 4.54609;

        return sourceUnits switch
        {
            "gallons" => sourceValue * gallonsToLitres,
            "cubicmetres" or "megagallons" => ConvertToGallons(sourceValue, sourceUnits) * gallonsToLitres,
            _ => throw new Exception($"Unit conversion not mapped - {sourceUnits} to gallons")
        };
    }
    
    private static double ConvertToGallons(
        double sourceValue,
        string sourceUnits)
    {
        const double litresToGallons = 0.21997;
        const double megaGallonsToGallons = 0.000_001;
        const double cubicMetresToGallons = 219.969;

        return sourceUnits switch
        {
            "litres" => sourceValue * litresToGallons,
            "cubicmetres" => sourceValue * cubicMetresToGallons,
            "megagallons" => sourceValue * megaGallonsToGallons,
            _ => throw new Exception($"Unit conversion not mapped - {sourceUnits} to gallons")
        };
    }
}