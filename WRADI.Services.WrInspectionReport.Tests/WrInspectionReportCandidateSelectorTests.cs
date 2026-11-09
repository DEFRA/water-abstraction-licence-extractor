using WRADI.Services.Cache.WrInspectionReport.Models;
using WRADI.Services.ProcessFile.WrInspectionReport;

namespace WRADI.Services.WrInspectionReport.Tests;

/// <summary>
/// Which discovered documents a run takes. Selection is on whether an object exists in the ingress
/// bucket, so the cases worth pinning are the ones where that disagrees with the DMS file name: a
/// Word document whose PDF has been converted, and a PDF that DMS lists but nobody ever collected.
/// </summary>
public class WrInspectionReportCandidateSelectorTests
{
    private const string Permit = "03284200261r01";
    private const string FileId = "aea48d21-abb6-426e-ab5e-5b88d7279fa9";

    private static InspectionReportFinderResult Candidate(
        string? fileName = "report.pdf",
        string? permitNumber = Permit,
        string? fileId = FileId) =>
        new()
        {
            PermitNumber = permitNumber,
            FileId = fileId,
            FileName = fileName
        };

    private static IReadOnlySet<string> Ingress(params string[] keys) =>
        WrInspectionReportCandidateSelector.ToIngressFileNameSet(keys);

    private static string KeyFor(InspectionReportFinderResult candidate) =>
        WrInspectionReportIngressFile.BuildFileName(candidate.PermitNumber!, candidate.FileId!);

    private static bool IsSelected(
        InspectionReportFinderResult candidate,
        IReadOnlySet<string> ingressFileNames) =>
        WrInspectionReportCandidateSelector.Select([candidate], ingressFileNames).Sendable.Count == 1;

    [Theory]
    [InlineData("Compliance Inspection WR51 - 032842002.doc")]
    [InlineData("Compliance Inspection WR51 - 032842002.docx")]
    public void WhenAWordDocumentHasBeenConvertedIntoIngress_ThenItIsSendable(string fileName)
    {
        // The DMS name says .doc; the object at the key is the converted PDF.
        var candidate = Candidate(fileName);

        Assert.True(IsSelected(candidate, Ingress(KeyFor(candidate))));
    }

    [Fact]
    public void WhenAPdfWasNeverCollected_ThenItIsNotSendable()
    {
        // Previously queued and failed one per file, with nothing said before the run started.
        var candidate = Candidate("Compliance Inspection WR51 - 032842002.pdf");

        Assert.False(IsSelected(candidate, Ingress()));
    }

    [Fact]
    public void WhenTheKeyDiffersOnlyByCase_ThenItIsStillFound()
    {
        var candidate = Candidate();

        Assert.True(IsSelected(candidate, Ingress(KeyFor(candidate).ToUpperInvariant())));
    }

    [Theory]
    [InlineData(null, FileId)]
    [InlineData("", FileId)]
    [InlineData(Permit, null)]
    [InlineData(Permit, "")]
    [InlineData(Permit, "not-a-guid")]
    public void WhenIdentityIsMissingOrUnparseable_ThenItIsNotSendable(string? permitNumber, string? fileId)
    {
        // No identity means no key to ask the bucket about, so this is checked before the lookup.
        var candidate = Candidate(permitNumber: permitNumber, fileId: fileId);

        Assert.False(WrInspectionReportCandidateSelector.HasIdentity(candidate));
        Assert.False(IsSelected(candidate, Ingress("wr51__anything__0.pdf")));
    }

    [Fact]
    public void WhenCandidatesAreSelected_ThenEachRejectionIsCountedUnderItsOwnReason()
    {
        var sendable = Candidate(fileId: "11111111-1111-4111-8111-111111111111");
        var notCollected = Candidate(fileId: "22222222-2222-4222-8222-222222222222");
        var noIdentity = Candidate(fileId: "not-a-guid");

        var selection = WrInspectionReportCandidateSelector.Select(
            [sendable, notCollected, noIdentity],
            Ingress(KeyFor(sendable)));

        Assert.Equal([sendable], selection.Sendable);
        Assert.Equal(1, selection.NotInIngress);
        Assert.Equal(1, selection.WithoutIdentity);
    }

    [Fact]
    public void WhenNothingIsInIngress_ThenNothingIsSendable()
    {
        var selection = WrInspectionReportCandidateSelector.Select([Candidate()], Ingress());

        Assert.Empty(selection.Sendable);
        Assert.Equal(1, selection.NotInIngress);
    }
}
