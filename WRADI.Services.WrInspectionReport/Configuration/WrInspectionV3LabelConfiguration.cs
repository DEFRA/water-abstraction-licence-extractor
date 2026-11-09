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
        .. ConditionRules(),
        RuleMeterDetailsBlock(),
        RuleNonCompliancesBlock(),
        RuleEnforcementResponse(),
        RuleActionsBlock(),
        RuleGeneralComments()
    ];

    /// <summary>
    /// Named with the shared provision field names, not V3-prefixed ones like the text fields
    /// above, so GetInOrderStatus resolves them with no mapping step. Safe only because the
    /// orchestrator replaces the whole label set for v3.
    ///
    /// Special conditions, land and charging factors have no v3 counterpart. Metering accuracy and
    /// maintenance records are not conditions - they sit in the repeating meter group.
    /// </summary>
    private static List<(string, List<LabelToMatch>)> ConditionRules() =>
    [
        KeyValueField("Source of supply", WrInspectionReportFieldNames.SourceOfSupply),
        KeyValueField("Purpose(s)", WrInspectionReportFieldNames.Purposes),
        KeyValueField("Point(s) of abstraction", WrInspectionReportFieldNames.PointOfAbstraction),
        KeyValueField("Means of abstraction", WrInspectionReportFieldNames.MeansOfAbstraction),
        KeyValueField("Period", WrInspectionReportFieldNames.Period),
        KeyValueField("Maximum quantity to be abstracted", WrInspectionReportFieldNames.Quantities),
        KeyValueField("Means of measurement", WrInspectionReportFieldNames.MeansOfMeasurement),
        KeyValueField("Records", WrInspectionReportFieldNames.Records),

        // "Returns" onto provision of information is a judgement call, not a name match.
        KeyValueField("Returns", WrInspectionReportFieldNames.ProvisionOfInformation),

        KeyValueField("Other further conditions", WrInspectionReportFieldNames.OtherProvisions),

        // Full keys, not stems: the extractor also emits truncated copies of these.
        KeyValueField(
            "Hands of Flow (HoF), Level (HoL) or prescribed flow",
            WrInspectionReportFieldNames.V3ConditionHandsOffFlow),
        KeyValueField(
            "Compensation or flow augmentation condition",
            WrInspectionReportFieldNames.V3ConditionCompensationOrAugmentation),
        KeyValueField(
            "Fish and eel screening conditions",
            WrInspectionReportFieldNames.V3ConditionFishAndEelScreening)
    ];

    private static (string, List<LabelToMatch>) KeyValueField(string key, string fieldName) =>
        (fieldName, [
            WrFluentRule
                .After(key)
                .Named(fieldName)
                .FromLetterAndKeyValueTable()
                .Build()]);

    /// AllowValueToWrapToNextLine was tried and appended the next field's label instead of the
    /// wrapped remainder, the same corruption that sank the attempt at v1's NameAndAddress.
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

    /// Left unsplit though the samples hold two names: the shared InspectingOfficer is one string.
    private static (string, List<LabelToMatch>) RuleInspectingOfficers() =>
        (WrInspectionReportFieldNames.V3InspectingOfficers, [
            WrFluentRule
                .After("Inspecting officer(s)")
                // Plural first, or the "(s)" is left on the front of the officer's name.
                .AlsoStartsWith("Inspecting officer")
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

    // Anchored on the line the first meter follows: the heading wraps, and anchoring on its
    // opening words began the capture mid-sentence.
    private static (string, List<LabelToMatch>) RuleMeterDetailsBlock() =>
        (WrInspectionReportFieldNames.V3MeterDetailsBlock, [
            WrFluentRule
                .Between("recorded here.", "Non-compliances")
                .Named(WrInspectionReportFieldNames.V3MeterDetailsBlock)
                .WholeLine()
                .NextLines(80)
                .Build()]);

    /// Captured raw, leading with the tail of the wrapped header. Every body row is empty in every
    /// sample, so nothing parses the columns until a filled example turns up.
    private static (string, List<LabelToMatch>) RuleNonCompliancesBlock() =>
        (WrInspectionReportFieldNames.V3NonCompliancesBlock, [
            WrFluentRule
                .Between("reference", "Enforcement response")
                .Named(WrInspectionReportFieldNames.V3NonCompliancesBlock)
                .WholeLine()
                .NextLines(30)
                .Build()]);

    private static (string, List<LabelToMatch>) RuleEnforcementResponse() =>
        (WrInspectionReportFieldNames.V3EnforcementResponse, [
            WrFluentRule
                .Between("assessment is as follows:", "Actions")
                .Named(WrInspectionReportFieldNames.V3EnforcementResponse)
                .WholeLine()
                .NextLines(6)
                .Build()]);

    /// The raw block, kept alongside the per-row read in WrInspectionV3KeyValueMatcher. Leads with
    /// the tail of the wrapped "Complete by" header.
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
                // The heading wraps in two places - 40 of 41 samples after "actions required",
                // one after "actions" - so both forms are anchored and the leftover is stripped.
                .AlsoStartsWith("General comments, details / dates of occupation changes, actions")
                .Remove([new TextToMatch("required etc.")])
                .Named(WrInspectionReportFieldNames.V3GeneralComments)
                .WholeLine()
                .NextLines(60)
                .EndsAt("Document Template Version")
                .Build()]);
}
