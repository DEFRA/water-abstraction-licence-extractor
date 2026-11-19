using WALE.ProcessFile.Core.Enums;
using WRADI.Core.AbstractionLicence.Enums;
using WRADI.Core.AbstractionLicence.Models;

namespace WRADI.Services.AbstractionLicence.Tests.Helper;

public class AbstractionLimitBuilder
{
    private double? _value = 100;
    private string? _units = "litres";
    private LimitPeriodType _periodType = LimitPeriodType.PerDay;
    private Point[]? _points;
    private Purpose[]? _purposes;
    private ContainedInInformation[]? _containedIn;

    public static AbstractionLimitBuilder A() => new();

    public static ContainedInInformation From(InformationSource source = InformationSource.Document) =>
        new() { Source = source };

    public AbstractionLimitBuilder Of(double? value, string? units = "litres")
    {
        _value = value;
        _units = units;
        return this;
    }

    public AbstractionLimitBuilder Per(LimitPeriodType periodType)
    {
        _periodType = periodType;
        return this;
    }

    public AbstractionLimitBuilder At(params string[] pointIds)
    {
        _points = pointIds.Select(id => new Point { Id = id, NaldId = $"N{id}" }).ToArray();
        return this;
    }

    public AbstractionLimitBuilder For(params string[] purposeIds)
    {
        _purposes = purposeIds.Select(id => new Purpose { Id = id }).ToArray();
        return this;
    }

    public AbstractionLimitBuilder SeenIn(params ContainedInInformation[] containedIn)
    {
        _containedIn = containedIn;
        return this;
    }

    public AbstractionLimit Build() => new()
    {
        Value = _value,
        Units = _units,
        PeriodType = _periodType,
        Points = _points,
        Purposes = _purposes,
        ContainedIn = _containedIn
    };

    public static AbstractionLimitGroup GroupOf(params AbstractionLimit[] limits) =>
        new() { Limits = limits.ToList() };

    public static AbstractionLimitGroup GroupSeenIn(
        ContainedInInformation containedIn,
        params AbstractionLimit[] limits) =>
        new() { ContainedIn = [containedIn], Limits = limits.ToList() };
}
