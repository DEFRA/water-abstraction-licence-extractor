using WRADI.Core.AbstractionLicence.Enums;
using WRADI.Core.AbstractionLicence.Models;

namespace WRADI.Services.AbstractionLicence.Tests.Helper;

public class LicenceSetBuilder
{
    private LicenceSetType _licenceSetType = LicenceSetType.LicencesGroupedByAbstractionLimits;
    private Licence[] _licences = [];
    private Aggregate[]? _aggregateSets;

    public static LicenceSetBuilder A() => new();

    public LicenceSetBuilder OfType(LicenceSetType licenceSetType)
    {
        _licenceSetType = licenceSetType;
        return this;
    }

    public LicenceSetBuilder Containing(params Licence[] licences)
    {
        _licences = licences;
        return this;
    }

    public LicenceSetBuilder WithAggregateSets(params Aggregate[] aggregateSets)
    {
        _aggregateSets = aggregateSets;
        return this;
    }

    public LicenceSet Build() => new()
    {
        LicenceSetType = _licenceSetType,
        Licences = _licences,
        AggregateSets = _aggregateSets
    };
}
