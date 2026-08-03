namespace WRADI.Core.AbstractionLicence.Models;

public class NaldDataQuantity
{
    public double? AnnualQty { get; init; }
    public char? AnnualQtyUsability { get; init; }
    public double? DailyQty { get; init; }
    public char? DailyQtyUsability { get; init; }
    public double? HourlyQty { get; init; }
    public char? HourlyQtyUsability { get; init; }
    public double? InstQty { get; init; }
    public char? InstQtyUsability { get; init; }
}