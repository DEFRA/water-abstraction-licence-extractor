using WRADI.Core.AbstractionLicence.Models;
using WRADI.DocumentType.AbstractionLicence.Converters;

namespace WRADI.Services.AbstractionLicence.Tests.UnitTests;

public class NaldPeriodDateTests
{
    [Fact]
    public void WhenThereIsNoNaldLine_ThenThereIsNoDate()
    {
        Assert.Null(AbstractionLicenceSchemaConverter.GetNaldPeriodStartDate(null, null));
        Assert.Null(AbstractionLicenceSchemaConverter.GetNaldPeriodEndDate(null, null));
    }

    [Fact]
    public void WhenThereAreNoPeriods_ThenThereIsNoDate()
    {
        var nald = NaldWith();

        Assert.Null(AbstractionLicenceSchemaConverter.GetNaldPeriodStartDate(nald, null));
        Assert.Null(AbstractionLicenceSchemaConverter.GetNaldPeriodEndDate(nald, null));
    }

    // Day and month are joined as written, so single digits are not padded.
    [Fact]
    public void WhenThereIsOnePeriod_ThenItsDaysAndMonthsAreJoined()
    {
        var nald = NaldWith(Period(1, 4, 31, 3));

        Assert.Equal("1/4", AbstractionLicenceSchemaConverter.GetNaldPeriodStartDate(nald, null));
        Assert.Equal("31/3", AbstractionLicenceSchemaConverter.GetNaldPeriodEndDate(nald, null));
    }

    // With one period it is taken whatever it holds; the null check only applies when choosing
    // between several.
    [Fact]
    public void WhenTheOnlyPeriodHasNoDays_ThenTheSeparatorIsStillReturned()
    {
        var nald = NaldWith(Period(null, null, null, null));

        Assert.Equal("/", AbstractionLicenceSchemaConverter.GetNaldPeriodStartDate(nald, null));
        Assert.Equal("/", AbstractionLicenceSchemaConverter.GetNaldPeriodEndDate(nald, null));
    }

    [Fact]
    public void WhenSeveralPeriodsExist_ThenTheFirstWithThatEndOfTheRangeIsUsed()
    {
        var nald = NaldWith(
            Period(null, null, null, null),
            Period(5, 5, 6, 6),
            Period(7, 7, 8, 8));

        Assert.Equal("5/5", AbstractionLicenceSchemaConverter.GetNaldPeriodStartDate(nald, null));
        Assert.Equal("6/6", AbstractionLicenceSchemaConverter.GetNaldPeriodEndDate(nald, null));
    }

    // Each end of the range is chosen independently, so the two dates can come from different
    // periods.
    [Fact]
    public void TheStartAndEndCanBeTakenFromDifferentPeriods()
    {
        var nald = NaldWith(
            Period(1, 1, null, null),
            Period(null, null, 2, 2));

        Assert.Equal("1/1", AbstractionLicenceSchemaConverter.GetNaldPeriodStartDate(nald, null));
        Assert.Equal("2/2", AbstractionLicenceSchemaConverter.GetNaldPeriodEndDate(nald, null));
    }

    // With more than one period the choice uses First rather than FirstOrDefault, so a set where
    // none carries that end of the range throws instead of returning nothing.
    [Fact]
    public void WhenSeveralPeriodsExistAndNoneHasThatEnd_ThenItThrows()
    {
        var nald = NaldWith(
            Period(null, null, 1, 1),
            Period(null, null, 2, 2));

        Assert.Throws<InvalidOperationException>(() =>
            AbstractionLicenceSchemaConverter.GetNaldPeriodStartDate(nald, null));
    }

    // The description parameter is not read by either method.
    [Fact]
    public void TheDescriptionMakesNoDifference()
    {
        var nald = NaldWith(Period(1, 4, 31, 3));

        Assert.Equal(
            AbstractionLicenceSchemaConverter.GetNaldPeriodStartDate(nald, null),
            AbstractionLicenceSchemaConverter.GetNaldPeriodStartDate(nald, "anything at all"));
    }

    private static NaldAbstractionData NaldWith(params NaldDataPeriod[] periods)
    {
        return new NaldAbstractionData { Periods = periods.ToList() };
    }

    private static NaldDataPeriod Period(int? startDay, int? startMonth, int? endDay, int? endMonth)
    {
        return new NaldDataPeriod
        {
            PeriodStartDay = startDay,
            PeriodStartMonth = startMonth,
            PeriodEndDay = endDay,
            PeriodEndMonth = endMonth
        };
    }
}
