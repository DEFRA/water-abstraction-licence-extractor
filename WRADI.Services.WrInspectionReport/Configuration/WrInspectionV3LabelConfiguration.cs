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
    /// v3 replaced v1's tick grid with a condition-and-outcome list, and each row of it is a
    /// key/value pair like any other field, so the outcomes need no new reading mechanism.
    ///
    /// Named with the shared provision field names rather than V3-prefixed ones, unlike the text
    /// fields above: GetInOrderStatus then resolves them with no mapping step. Safe because the
    /// orchestrator replaces the whole label set for v3, so a v1 rule of the same name is never
    /// also present.
    ///
    /// Three of v1's thirteen - special conditions, land, and charging factors - have no v3
    /// counterpart and stay unmatched, which is correct: v3 does not print them.
    ///
    /// The metering accuracy and maintenance records rows are deliberately absent. They sit inside
    /// the repeating meter group rather than the conditions list, and metering accuracy holds
    /// evidence text ("Flow Meter Verification full report seen dated 31/03/2025 - meter accuracy
    /// 6.33%") rather than a verdict, so neither is a provision. Both go to the meter record.
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

        // v3's "Returns" is the condition requiring the holder to send us their figures, which is
        // what v1 called provision of information. A judgement call, not a name match.
        KeyValueField("Returns", WrInspectionReportFieldNames.ProvisionOfInformation),

        KeyValueField("Other further conditions", WrInspectionReportFieldNames.OtherProvisions),

        // The full keys, not their stems: the extractor also emits truncated copies ("Compensation
        // or flow", "Metering accuracy, calibration or") and an exact match beats a stem match.
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
    /// The non-compliances table. The capture yields only the tail of the wrapped header
    /// ("category date").
    ///
    /// Its columns are known - the extractor reads the header cleanly as condition reference,
    /// condition breached, compliance category, CCS reference and breach date - but every body
    /// row is empty in every consultation sample, so the value formats are unobserved and a
    /// filled example is needed before anything parses or normalises them.
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
    /// The actions table: Reference, Type, Action, Complete by. Captured as raw text, which leads
    /// with the tail of the wrapped "Complete by" header.
    ///
    /// A row read is available and unbuilt, not blocked: the extractor yields this as a clean
    /// four-column table, one row per action, with a filled action's whole text in a single cell.
    /// Two things stand in the way. KeyValueTableHelper reads two-column tables only, and the
    /// extractor also emits a fragmented duplicate of this table in which one action spans a row
    /// per line, so picking the clean copy needs a rule. Unfilled rows read "Choose an item.".
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
