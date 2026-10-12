using WALE.ProcessFile.Core.Models;
using WRADI.DocumentType.WrInspectionReport.Constants;
using WRADI.DocumentType.WrInspectionReport.Models.Configuration;

namespace WRADI.DocumentType.WrInspectionReport.Configuration;

public static class WrInspectionV3LabelConfiguration
{
    public static List<(string LabelGroupName, List<LabelToMatch> Labels)> GetLabels() =>
    [
        KeyValueField("Document Template Version", WrInspectionReportFieldNames.V3DocumentTemplateVersion),

        KeyValueField("Licence number", WrInspectionReportFieldNames.V3LicenceNumber),
        KeyValueField("Licence criticality", WrInspectionReportFieldNames.V3LicenceCriticality),
        RuleLicenceHolderName(),
        RuleLicenceHolderAddress(),
        KeyValueField("Site contact number", WrInspectionReportFieldNames.V3SiteContactNumber),
        KeyValueField("Site contact email", WrInspectionReportFieldNames.V3SiteContactEmail),
        RuleSiteAddress(),

        KeyValueField("Assessment type", WrInspectionReportFieldNames.V3AssessmentType),
        RulePersonMetWith(),
        KeyValueField("Job title or role", WrInspectionReportFieldNames.V3JobTitleOrRole),
        KeyValueField("Assessment date", WrInspectionReportFieldNames.V3AssessmentDate),
        KeyValueField("Inspection start time", WrInspectionReportFieldNames.V3InspectionStartTime),
        KeyValueField("Inspection end time", WrInspectionReportFieldNames.V3InspectionEndTime),
        RuleInspectingOfficers(),

        RuleLicenceConditionsBlock(),
        RuleMeterDetailsBlock(),
        RuleNonCompliancesBlock(),
        RuleEnforcementResponse(),
        RuleActionsBlock(),
        RuleGeneralComments()
    ];

    private static (string, List<LabelToMatch>) KeyValueField(string key, string fieldName) =>
        (fieldName, [
            WrFluentRule
                .After(key)
                .Named(fieldName)
                .FromLetterAndKeyValueTable()
                .Build()]);

    /// <summary>
    /// AllowValueToWrapToNextLine was tried and appended the NEXT field's label rather than the
    /// wrapped remainder - the same corruption that sank an earlier attempt at v1's NameAndAddress.
    /// </summary>
    private static (string, List<LabelToMatch>) RuleLicenceHolderName() =>
        (WrInspectionReportFieldNames.V3LicenceHolderName, [
            WrFluentRule
                .After("Licence Holder Name")
                .Named(WrInspectionReportFieldNames.V3LicenceHolderName)
                .FromLetterAndKeyValueTable()
                .NextLines(2)
                .EndsAt("Licence Holder address")
                .Build()]);

    private static (string, List<LabelToMatch>) RuleLicenceHolderAddress() =>
        (WrInspectionReportFieldNames.V3LicenceHolderAddress, [
            WrFluentRule
                .Between("Licence Holder address", "Site contact number")
                .Named(WrInspectionReportFieldNames.V3LicenceHolderAddress)
                .FromLetterAndKeyValueTable()
                .NextLines(8)
                .AlsoEndsAtLineStart("Site contact email", "Site address")
                .Build()]);

    private static (string, List<LabelToMatch>) RuleSiteAddress() =>
        (WrInspectionReportFieldNames.V3SiteAddress, [
            WrFluentRule
                .Between("Site address", "Compliance assessment information")
                .Named(WrInspectionReportFieldNames.V3SiteAddress)
                .FromLetterAndKeyValueTable()
                .NextLines(8)
                .AlsoEndsAtLineStart("Assessment type")
                .Build()]);

    private static (string, List<LabelToMatch>) RulePersonMetWith() =>
        (WrInspectionReportFieldNames.V3PersonMetWith, [
            WrFluentRule
                .After("Person met with/contacted")
                .Named(WrInspectionReportFieldNames.V3PersonMetWith)
                .FromLetterAndKeyValueTable()
                .NextLines(2)
                .EndsAt("Job title or role")
                .Build()]);

    /// <summary>
    /// Multi-valued in the samples ("Bekah Flynn &amp; Anna Pursey") and left unsplit: the shared
    /// model's InspectingOfficer is a single string, so there is nowhere for a second name to go.
    /// </summary>
    private static (string, List<LabelToMatch>) RuleInspectingOfficers() =>
        (WrInspectionReportFieldNames.V3InspectingOfficers, [
            WrFluentRule
                .After("Inspecting officer(s)")
                .Named(WrInspectionReportFieldNames.V3InspectingOfficers)
                .FromLetterAndKeyValueTable()
                .NextLines(2)
                .EndsAt("Licence conditions")
                .Build()]);

    private static (string, List<LabelToMatch>) RuleLicenceConditionsBlock() =>
        (WrInspectionReportFieldNames.V3LicenceConditionsBlock, [
            WrFluentRule
                .Between(
                    "Licence conditions assessed and the outcome of the compliance assessment.",
                    "Meter details")
                .Named(WrInspectionReportFieldNames.V3LicenceConditionsBlock)
                .WholeLine()
                .NextLines(40)
                .Build()]);

    // The heading sentence wraps, so anchoring on its opening words began the capture mid-sentence
    // ("recorded here." led the block). Anchored instead on the line the first meter follows.
    private static (string, List<LabelToMatch>) RuleMeterDetailsBlock() =>
        (WrInspectionReportFieldNames.V3MeterDetailsBlock, [
            WrFluentRule
                .Between("recorded here.", "Non-compliances")
                .Named(WrInspectionReportFieldNames.V3MeterDetailsBlock)
                .WholeLine()
                .NextLines(80)
                .Build()]);

    /// <summary>
    /// The non-compliances table, empty in every consultation sample, so its row shape is
    /// unverified. The capture yields only the tail of the wrapped header ("category date").
    /// </summary>
    private static (string, List<LabelToMatch>) RuleNonCompliancesBlock() =>
        (WrInspectionReportFieldNames.V3NonCompliancesBlock, [
            WrFluentRule
                .Between("reference", "Enforcement response")
                .Named(WrInspectionReportFieldNames.V3NonCompliancesBlock)
                .WholeLine()
                .NextLines(30)
                .Build()]);

    /// <summary>
    /// A single dropdown value below the enforcement preamble. Unfilled in the consultation
    /// samples, where it reads "Choose an item.".
    /// </summary>
    private static (string, List<LabelToMatch>) RuleEnforcementResponse() =>
        (WrInspectionReportFieldNames.V3EnforcementResponse, [
            WrFluentRule
                .Between("assessment is as follows:", "Actions")
                .Named(WrInspectionReportFieldNames.V3EnforcementResponse)
                .WholeLine()
                .NextLines(6)
                .Build()]);

    /// <summary>
    /// The actions table: Reference, Type, Action, Complete by. Two of the six consultation
    /// samples have filled rows, the rest are unfilled dropdowns reading "Choose an item.".
    /// Captured whole: the capture also leads with the tail of the wrapped "Complete by" header,
    /// and splitting it into rows is unbuilt.
    /// </summary>
    private static (string, List<LabelToMatch>) RuleActionsBlock() =>
        (WrInspectionReportFieldNames.V3ActionsBlock, [
            WrFluentRule
                .Between("Complete", "Observations")
                .Named(WrInspectionReportFieldNames.V3ActionsBlock)
                .WholeLine()
                .NextLines(40)
                .Build()]);

    private static (string, List<LabelToMatch>) RuleGeneralComments() =>
        (WrInspectionReportFieldNames.V3GeneralComments, [
            WrFluentRule
                .After("General comments, details / dates of occupation changes, actions required")
                .Named(WrInspectionReportFieldNames.V3GeneralComments)
                .WholeLine()
                .NextLines(60)
                .EndsAt("Document Template Version")
                .Build()]);
}
