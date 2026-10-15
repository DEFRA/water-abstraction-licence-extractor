using System.Text.Json;
using WRADI.DocumentType.WrInspectionReport.Enums;
using WRADI.DocumentType.WrInspectionReport.Services;

namespace WRADI.Services.WrInspectionReport.Tests;

/// <summary>
/// Direct unit tests for the three pure helpers behind every row of the date-driven report list:
/// which of the four dates a row is sorted under, whether an extracted date is implausible, and
/// the completeness percentage. No MatchesResult or PDF fixture needed.
///
/// Worth pinning because all three fail quietly. A wrong date source shows a plausible date from
/// the wrong origin, a missed warning shows a misread year as a successful extraction, and a
/// completeness change moves every row by a round 10%.
/// </summary>
public class WrInspectionReportSummaryBuilderTests
{
    private static readonly DateOnly Inspection = new(2024, 6, 10);
    private static readonly DateOnly Form = new(2024, 6, 11);
    private static readonly DateOnly DmsDocument = new(2024, 6, 12);
    private static readonly DateOnly DmsUpload = new(2024, 6, 13);

    public class PickSortDateTests
    {
        [Fact]
        public void WhenTheDocumentYieldedAnInspectionDate_ThenItWinsOverEveryFilingDate()
        {
            var (date, source) = WrInspectionReportSummaryBuilder.PickSortDate(
                Inspection, Form, (DmsDocument, DmsUpload));

            Assert.Equal(Inspection, date);
            Assert.Equal(WrInspectionReportDateSource.Extracted, source);
        }

        [Fact]
        public void WhenNoInspectionDate_ThenTheFormsOwnDateIsPreferredToDms()
        {
            var (date, source) = WrInspectionReportSummaryBuilder.PickSortDate(
                null, Form, (DmsDocument, DmsUpload));

            Assert.Equal(Form, date);
            Assert.Equal(WrInspectionReportDateSource.Metadata, source);
        }

        [Fact]
        public void WhenTheDocumentYieldedNoDate_ThenTheDmsDocumentDateStandsIn()
        {
            var (date, source) = WrInspectionReportSummaryBuilder.PickSortDate(
                null, null, (DmsDocument, DmsUpload));

            Assert.Equal(DmsDocument, date);
            Assert.Equal(WrInspectionReportDateSource.DmsDocument, source);
        }

        /// The last resort, and the one that only becomes reachable once the finder persists
        /// upload_date - DMS holds one for every file, including those with no document date.
        [Fact]
        public void WhenOnlyTheUploadDateIsKnown_ThenItIsUsedAndLabelledAsSuch()
        {
            var (date, source) = WrInspectionReportSummaryBuilder.PickSortDate(
                null, null, (null, DmsUpload));

            Assert.Equal(DmsUpload, date);
            Assert.Equal(WrInspectionReportDateSource.DmsUpload, source);
        }

        [Fact]
        public void WhenNoSourceHasADate_ThenNoneRatherThanAFabricatedDate()
        {
            var (date, source) = WrInspectionReportSummaryBuilder.PickSortDate(
                null, null, (null, null));

            Assert.Null(date);
            Assert.Equal(WrInspectionReportDateSource.None, source);
        }
    }

    public class CheckDateTests
    {
        [Fact]
        public void WhenThereIsNoExtractedDate_ThenNoWarning()
        {
            Assert.Equal(
                WrInspectionReportDateWarning.None,
                WrInspectionReportSummaryBuilder.CheckDate(null, DmsDocument));
        }

        [Fact]
        public void WhenTheInspectionDateIsInTheFuture_ThenFutureDate()
        {
            var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);

            Assert.Equal(
                WrInspectionReportDateWarning.FutureDate,
                WrInspectionReportSummaryBuilder.CheckDate(tomorrow, null));
        }

        [Fact]
        public void WhenTodayIsTheInspectionDate_ThenNoWarning()
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            Assert.Equal(
                WrInspectionReportDateWarning.None,
                WrInspectionReportSummaryBuilder.CheckDate(today, null));
        }

        [Theory]
        [InlineData(1959, true)]
        [InlineData(1960, false)]
        public void WhenTheYearPredatesTheCorpus_ThenBeforeCorpusEra(int year, bool expectWarning)
        {
            var warning = WrInspectionReportSummaryBuilder.CheckDate(new DateOnly(year, 5, 1), null);

            Assert.Equal(
                expectWarning
                    ? WrInspectionReportDateWarning.BeforeCorpusEra
                    : WrInspectionReportDateWarning.None,
                warning);
        }

        [Fact]
        public void WhenTheInspectionPostdatesFiling_ThenAfterFiling()
        {
            var warning = WrInspectionReportSummaryBuilder.CheckDate(
                DmsDocument.AddDays(2), DmsDocument);

            Assert.Equal(WrInspectionReportDateWarning.AfterFiling, warning);
        }

        /// The British Summer Time grace day: DMS records the document a day early for most of the
        /// year, so one day ahead of filing is normal and must not warn.
        [Fact]
        public void WhenTheInspectionIsOneDayAfterFiling_ThenNoWarning()
        {
            var warning = WrInspectionReportSummaryBuilder.CheckDate(
                DmsDocument.AddDays(1), DmsDocument);

            Assert.Equal(WrInspectionReportDateWarning.None, warning);
        }

        /// A future date is reported as such even though it also postdates filing - the useful
        /// warning is the one a human can act on.
        [Fact]
        public void WhenADateIsBothFutureAndAfterFiling_ThenFutureDateIsReported()
        {
            var warning = WrInspectionReportSummaryBuilder.CheckDate(
                DateOnly.FromDateTime(DateTime.UtcNow).AddYears(18), DmsDocument);

            Assert.Equal(WrInspectionReportDateWarning.FutureDate, warning);
        }
    }

    public class ComputeCompletenessTests
    {
        /// Ten sections, so each one carrying content is worth a round 10%.
        private const int SectionCount = 10;

        private static int Completeness(string json)
        {
            using var document = JsonDocument.Parse(json);

            return WrInspectionReportSummaryBuilder.ComputeCompleteness(document.RootElement);
        }

        [Fact]
        public void WhenNothingWasExtracted_ThenZero()
        {
            Assert.Equal(0, Completeness("{}"));
        }

        [Fact]
        public void WhenOneSectionHasContent_ThenOneTenth()
        {
            Assert.Equal(100 / SectionCount, Completeness("""{"licenceNumber":"7/34/06/G/0027"}"""));
        }

        [Fact]
        public void WhenEverySectionHasContent_ThenOneHundred()
        {
            var sections = new[]
            {
                "licenceNumber", "licenceNumberCleaned", "inspectionClass", "address", "metWith",
                "inspectingOfficer", "inspectionDate", "licenceProvisions", "measurementDetails",
                "generalComments"
            };

            var json = "{" + string.Join(",", sections.Select(name => $"\"{name}\":\"x\"")) + "}";

            Assert.Equal(100, Completeness(json));
        }

        /// The regression this guards: DidntMatch is the absence of a finding but serialises as a
        /// non-empty string, so counting it gave every document a floor of 10%.
        [Fact]
        public void WhenASectionHoldsOnlyDidntMatch_ThenItDoesNotCount()
        {
            Assert.Equal(0, Completeness("""{"licenceProvisions":{"sourceOfSupply":"DidntMatch"}}"""));
        }

        [Fact]
        public void WhenASectionMixesDidntMatchWithARealValue_ThenItCounts()
        {
            Assert.Equal(
                100 / SectionCount,
                Completeness("""{"licenceProvisions":{"sourceOfSupply":"DidntMatch","conformance":"InOrder"}}"""));
        }

        /// Deliberately not the same as DidntMatch: a rule did match here and the value was
        /// genuinely blank, which is a finding about the document.
        [Fact]
        public void WhenAValueIsWhitespace_ThenItDoesNotCount()
        {
            Assert.Equal(0, Completeness("""{"metWith":{"name":"   "}}"""));
        }

        [Fact]
        public void WhenASectionIsNull_ThenItDoesNotCount()
        {
            Assert.Equal(0, Completeness("""{"generalComments":null}"""));
        }

        [Fact]
        public void WhenASectionIsAnArrayWithOneRealEntry_ThenItCounts()
        {
            Assert.Equal(
                100 / SectionCount,
                Completeness("""{"measurementDetails":{"meters":[{"meterMake":"Siemens"}]}}"""));
        }

        /// Sections outside the measured set cannot inflate the figure.
        [Fact]
        public void WhenOnlyUnmeasuredSectionsHaveContent_ThenZero()
        {
            Assert.Equal(0, Completeness("""{"metadata":{"template":"t1"},"formSentTo":"someone"}"""));
        }
    }
}
