namespace WALE.ProcessFile.Core.Helpers;

public static class UnitHelper
{
    public static double Convert(double sourceValue, string sourceUnits, string destinationUnits)
    {
        sourceUnits = LoweredUnits(sourceUnits);
        destinationUnits = LoweredUnits(destinationUnits);

        return destinationUnits switch
        {
            Gallons => ConvertToGallons(sourceValue, sourceUnits),
            MegaGallons => ConvertToMegaGallons(sourceValue, sourceUnits),
            Litres => ConvertToLitres(sourceValue, sourceUnits),
            MegaLitres => ConvertToMegaLitres(sourceValue, sourceUnits),
            CubicMetres => ConvertToCubicMetres(sourceValue, sourceUnits),
            ThousandCubicMetres => ConvertToThousandCubicMetres(sourceValue, sourceUnits),
            _ => throw new Exception($"Unit conversion not mapped - {sourceUnits} to {destinationUnits}")
        };
    }
    
    private const string MillionGallons = "milliongallons";
    private const string MegaGallons = "megagallons";
    private const string Gallons = "gallons";
    private const string Litres = "litres";
    private const string MegaLitres = "megalitres";
    private const string CubicMetres = "cubicmetres";
    private const string ThousandCubicMetres = "thousandcubicmetres";

    private static string LoweredUnits(string units)
    {
        units = units.Replace(" ", string.Empty).ToLower();
        if (units == MillionGallons)
        {
            units = MegaGallons;
        }
        
        return units;
    }
    
    private static double ConvertToCubicMetres(double sourceValue, string sourceUnits)
    {
        const double gallonsToCubicMetres = 0.00454609;
        const double litresToCubicMetres = 0.001;
        const double thousandCubicMetresToCubicMetres = 0.001;
        
        return sourceUnits switch
        {
            Gallons => sourceValue * gallonsToCubicMetres,
            MegaGallons => ConvertToGallons(sourceValue, sourceUnits) * gallonsToCubicMetres,
            Litres => sourceValue * litresToCubicMetres,
            MegaLitres => ConvertToLitres(sourceValue, sourceUnits) * litresToCubicMetres,            
            CubicMetres => sourceValue,
            ThousandCubicMetres => sourceValue * thousandCubicMetresToCubicMetres,
            _ => throw new Exception($"Unit conversion not mapped - {sourceUnits} to cubic metres")
        };
    }
    
    private static double ConvertToThousandCubicMetres(double sourceValue, string sourceUnits)
    {
        const double cubicMetresToThousandCubicMetres = 1_000;
        return ConvertToCubicMetres(sourceValue, sourceUnits) * cubicMetresToThousandCubicMetres;
    }
    
    private static double ConvertToLitres(double sourceValue, string sourceUnits)
    {
        const double gallonsToLitres = 4.54609;
        const double cubicMetersToLitres = 1_000;
        const double megaLitresToLitres = 1_000_000;

        return sourceUnits switch
        {
            Gallons => sourceValue * gallonsToLitres,
            MegaGallons => ConvertToGallons(sourceValue, sourceUnits) * gallonsToLitres,
            Litres => sourceValue,
            MegaLitres => sourceValue * megaLitresToLitres,
            CubicMetres => sourceValue * cubicMetersToLitres,
            ThousandCubicMetres => ConvertToCubicMetres(sourceValue, sourceUnits) * cubicMetersToLitres,
            _ => throw new Exception($"Unit conversion not mapped - {sourceUnits} to litres")
        };
    }

    private static double ConvertToMegaLitres(double sourceValue, string sourceUnits)
    {
        const double litresToMegaLitres = 1_000_000;
        return ConvertToLitres(sourceValue, sourceUnits) * litresToMegaLitres;
    }
    
    private static double ConvertToGallons(double sourceValue, string sourceUnits)
    {
        const double litresToGallons = 0.21997;
        const double megaGallonsToGallons = 0.000_001;
        const double cubicMetresToGallons = 219.969;

        return sourceUnits switch
        {
            Gallons => sourceValue,
            MegaGallons => sourceValue * megaGallonsToGallons,
            Litres => sourceValue * litresToGallons,
            MegaLitres => ConvertToLitres(sourceValue, sourceUnits) * litresToGallons,
            CubicMetres => sourceValue * cubicMetresToGallons,
            ThousandCubicMetres => ConvertToCubicMetres(sourceValue, sourceUnits) * cubicMetresToGallons,
            _ => throw new Exception($"Unit conversion not mapped - {sourceUnits} to gallons")
        };
    }
    
    private static double ConvertToMegaGallons(double sourceValue, string sourceUnits)
    {
        const double gallonsToMegaGallons = 1_000_000;
        return ConvertToGallons(sourceValue, sourceUnits) * gallonsToMegaGallons;
    }
}