using WALE.ProcessFile.Core.Enums;
using WRADI.Core.AbstractionLicence.Models;

namespace WRADI.Services.AbstractionLicence.Tests.Helper;

/// <summary>
/// Aggregates stays null unless Holding is called, where AbstractionLimits itself initialises
/// it to an empty array. The rules skip a null and walk an empty one.
/// </summary>
public class LicenceBuilder
{
    private ScrapeStatus _status = ScrapeStatus.Ok;
    private string? _licenceNumber;
    private LinkedLicence[] _linkedLicences = [];
    private Aggregate[]? _aggregates;

    public static LicenceBuilder A() => new();

    public static LinkedLicence LinkTo(string licenceNumber, ContainedInInformation[]? containedIn = null) =>
        new()
        {
            LicenceNumber = licenceNumber,
            ContainedIn = containedIn
        };

    public LicenceBuilder Numbered(string licenceNumber)
    {
        _licenceNumber = licenceNumber;
        return this;
    }

    public LicenceBuilder ThatDidNotExtractCleanly(ScrapeStatus status)
    {
        _status = status;
        return this;
    }

    public LicenceBuilder LinkedTo(params LinkedLicence[] linkedLicences)
    {
        _linkedLicences = linkedLicences;
        return this;
    }

    public LicenceBuilder Holding(params Aggregate[] aggregates)
    {
        _aggregates = aggregates;
        return this;
    }

    public Licence Build() => new()
    {
        Status = _status,
        LicenceNumber = _licenceNumber == null
            ? null
            : new ValueWithConfidence<string>(_licenceNumber, null, null),
        LinkedLicences = _linkedLicences,
        AbstractionLimits = new AbstractionLimits { Aggregates = _aggregates }
    };
}
