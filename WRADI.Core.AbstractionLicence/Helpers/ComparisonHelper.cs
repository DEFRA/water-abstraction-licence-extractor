namespace WRADI.Core.AbstractionLicence.Helpers;

public static class ComparisonHelper
{
    public static bool AreValuesEqual(double? value1, double? value2, double maxTolerance = 0.01)
    {
        if (value1 == null || value2 == null)
        {
            return false;
        }

        var percentageDiff = Math.Abs(100.0 - (100.0 / value1.Value) * value2.Value);
        return percentageDiff >= 0 && percentageDiff <= maxTolerance;
    }
}