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
                .FromLetterAndTableGrid()
                .Build()]);

    /// <summary>
    /// KNOWN GAP: the value wraps and only the first line is read. AllowValueToWrapToNextLine was
    /// tried and appended the NEXT field's label rather than the wrapped remainder - the same
    /// corruption that sank an earlier attempt at v1's NameAndAddress.
    /// </summary>
    private static (string, List<LabelToMatch>) RuleLicenceHolderName() =>
        (WrInspectionReportFieldNames.V3LicenceHolderName, [
            WrFluentRule
                .After("Licence Holder Name")
                .Named(WrInspectionReportFieldNames.V3LicenceHolderName)
                .FromLetterAndTableGrid()
                .NextLines(2)
                .EndsAt("Licence Holder address")
                .Build()]);

    private static (string, List<LabelToMatch>) RuleLicenceHolderAddress() =>
        (WrInspectionReportFieldNames.V3LicenceHolderAddress, [
            WrFluentRule
                .Between("Licence Holder address", "Site contact number")
                .Named(WrInspectionReportFieldNames.V3LicenceHolderAddress)
                .FromLetterAndTableGrid()
                .NextLines(8)
                .AlsoEndsAtLineStart("Site contact email", "Site address")
                .Build()]);

    private static (string, List<LabelToMatch>) RuleSiteAddress() =>
        (WrInspectionReportFieldNames.V3SiteAddress, [
            WrFluentRule
                .Between("Site address", "Compliance assessment information")
                .Named(WrInspectionReportFieldNames.V3SiteAddress)
                .FromLetterAndTableGrid()
                .NextLines(8)
                .AlsoEndsAtLineStart("Assessment type")
                .Build()]);

    private static (string, List<LabelToMatch>) RulePersonMetWith() =>
        (WrInspectionReportFieldNames.V3PersonMetWith, [
            WrFluentRule
                .After("Person met with/contacted")
                .Named(WrInspectionReportFieldNames.V3PersonMetWith)
                .FromLetterAndTableGrid()
                .NextLines(2)
                .EndsAt("Job title or role")
                .Build()]);

    /// <summary>Multi-valued in the samples, left as read for the converter to split.</summary>
    private static (string, List<LabelToMatch>) RuleInspectingOfficers() =>
        (WrInspectionReportFieldNames.V3InspectingOfficers, [
            WrFluentRule
                .After("Inspecting officer(s)")
                .Named(WrInspectionReportFieldNames.V3InspectingOfficers)
                .FromLetterAndTableGrid()
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
    /// The non-compliances table, empty in every consultation sample (header row only), so its
    /// row shape is unverified. Captured whole until a filled example exists.
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
    /// The actions table: Reference, Type, Action, Complete by. Every row is an unfilled dropdown
    /// in the consultation samples, so like the non-compliances table its filled shape is
    /// unverified and it is captured whole for now.
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
