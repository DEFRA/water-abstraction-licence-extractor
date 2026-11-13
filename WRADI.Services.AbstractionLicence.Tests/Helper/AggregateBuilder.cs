using WRADI.Core.AbstractionLicence.Enums;
using WRADI.Core.AbstractionLicence.Models;

namespace WRADI.Services.AbstractionLicence.Tests.Helper;

public class AggregateBuilder
{
    private string? _sourceLicenceNumber = "1/2/3";
    private string[]? _linkedLicences = [];
    private List<AbstractionLimit> _limits = [new AbstractionLimit { Value = 100 }];
    private PrimaryType _primaryType = PrimaryType.LicenceToLicence;
    private SubType _subType = SubType.PointToPoint;
    private ContainedInInformation[]? _containedIn;
    private AggregateVariation[]? _variations;
    private string? _licenceSetId;

    public static AggregateBuilder An() => new();

    public static ContainedInInformation SeenIn(
        string? sectionName = null,
        int? lineNumber = null,
        InformationDirection? direction = null,
        string? linkReason = null) =>
        new()
        {
            Source = InformationSource.Document,
            SectionName = sectionName,
            LineNumber = lineNumber,
            Direction = direction,
            LinkReason = linkReason
        };

    public AggregateBuilder For(string? licenceNumber)
    {
        _sourceLicenceNumber = licenceNumber;
        return this;
    }

    public AggregateBuilder LinkedTo(params string[] licenceNumbers)
    {
        _linkedLicences = licenceNumbers;
        return this;
    }

    public AggregateBuilder WithNullLinkedLicences()
    {
        _linkedLicences = null;
        return this;
    }

    public AggregateBuilder Limited(params double?[] values)
    {
        _limits = values.Select(value => new AbstractionLimit { Value = value }).ToList();
        return this;
    }

    public AggregateBuilder OfType(PrimaryType primaryType, SubType subType)
    {
        _primaryType = primaryType;
        _subType = subType;
        return this;
    }

    public AggregateBuilder SightedIn(params ContainedInInformation[] containedIn)
    {
        _containedIn = containedIn;
        return this;
    }

    public AggregateBuilder Varying(params AggregateVariation[] variations)
    {
        _variations = variations;
        return this;
    }

    public AggregateBuilder InLicenceSet(string licenceSetId)
    {
        _licenceSetId = licenceSetId;
        return this;
    }

    public Aggregate Build() => Fill(new Aggregate());

    public AggregateVariation BuildVariation(string? difference = null)
    {
        var variation = Fill(new AggregateVariation());
        variation.Difference = difference;

        return variation;
    }

    private T Fill<T>(T aggregate) where T : Aggregate
    {
        aggregate.SourceLicenceNumber = _sourceLicenceNumber;
        aggregate.PrimaryType = _primaryType;
        aggregate.SubType = _subType;
        aggregate.LinkedLicences = _linkedLicences;
        aggregate.Limits = _limits;
        aggregate.ContainedIn = _containedIn;
        aggregate.Variations = _variations;
        aggregate.LicenceSetId = _licenceSetId;

        return aggregate;
    }
}
