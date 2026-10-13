namespace WRADI.DocumentType.WrInspectionReport.Constants;

public static class WrInspectionReportFieldNames
{
    public const string SourceOfSupply = nameof(SourceOfSupply);
    public const string PointOfAbstraction = nameof(PointOfAbstraction);
    public const string MeansOfAbstraction = nameof(MeansOfAbstraction);
    public const string Purposes = nameof(Purposes);
    public const string Period = nameof(Period);
    public const string Quantities = nameof(Quantities);
    public const string MeansOfMeasurement = nameof(MeansOfMeasurement);
    public const string Records = nameof(Records);
    public const string ProvisionOfInformation = nameof(ProvisionOfInformation);
    public const string SpecialConditions = nameof(SpecialConditions);
    public const string Land = nameof(Land);
    public const string ChargingFactors = nameof(ChargingFactors);
    public const string OtherProvisions = nameof(OtherProvisions);

    public const string LicenceNumber = nameof(LicenceNumber);
    public const string MetWith = nameof(MetWith);
    public const string InspectingOfficer = nameof(InspectingOfficer);
    public const string SiteAddress = nameof(SiteAddress);
    public const string InspectionClass = nameof(InspectionClass);
    public const string TelephoneNumber = nameof(TelephoneNumber);
    public const string Position = nameof(Position);
    public const string Time = nameof(Time);
    public const string NameAndAddress = nameof(NameAndAddress);

    public const string MeterName = nameof(MeterName);
    public const string MeterMake = nameof(MeterMake);
    public const string SerialNumber = nameof(SerialNumber);
    public const string MeterAssetNumber = nameof(MeterAssetNumber);
    public const string Reading = nameof(Reading);
    public const string FlowRate = nameof(FlowRate);
    public const string Units = nameof(Units);
    public const string Other = nameof(Other);
    public const string CertificatesOfRecords = nameof(CertificatesOfRecords);
    public const string DateOfCertification = nameof(DateOfCertification);
    public const string Calibration = nameof(Calibration);
    public const string Verification = nameof(Verification);
    public const string SpotCheckResult = nameof(SpotCheckResult);
    public const string Conformance = nameof(Conformance);
    public const string FlowVerification = nameof(FlowVerification);
    public const string MeterVerification = nameof(MeterVerification);
    public const string WhereKept = nameof(WhereKept);

    public const string FormSentTo = nameof(FormSentTo);
    public const string Date = nameof(Date);
    public const string DocumentTemplateVersion = nameof(DocumentTemplateVersion);
    public const string DocumentHeader = nameof(DocumentHeader);

    public const string TemplateMarkerV3 = nameof(TemplateMarkerV3);
    public const string TemplateMarkerT4 = nameof(TemplateMarkerT4);
    public const string TemplateMarkerT6 = nameof(TemplateMarkerT6);
    public const string TemplateMarkerT7 = nameof(TemplateMarkerT7);
    public const string TemplateMarkerImpounding = nameof(TemplateMarkerImpounding);
    public const string TemplateMarkerBaselineComments = nameof(TemplateMarkerBaselineComments);
    public const string TemplateMarkerAlternateComments = nameof(TemplateMarkerAlternateComments);

    public const string GeneralComments = nameof(GeneralComments);
    public const string InspectionDate = nameof(InspectionDate);
    public const string Email = nameof(Email);

    public const string MaintenanceLine = nameof(MaintenanceLine);
    public const string MaintenanceLineMaintenance = MaintenanceLine + "Maintenance";
    public const string MaintenanceLineMaintenanceYes = MaintenanceLine + "MaintenanceYes";
    public const string MaintenanceLineMaintenanceNo = MaintenanceLine + "MaintenanceNo";
    public const string MaintenanceLineFrequency = MaintenanceLine + "Frequency";
    public const string MaintenanceLineByWhom = MaintenanceLine + "ByWhom";

    public const string ReadingsTakenLine = nameof(ReadingsTakenLine);
    public const string ReadingsTakenLineReadingsTaken = ReadingsTakenLine + "ReadingsTaken";
    public const string ReadingsTakenLineReadingsTakenYes = ReadingsTakenLine + "ReadingsTakenYes";
    public const string ReadingsTakenLineReadingsTakenNo = ReadingsTakenLine + "ReadingsTakenNo";
    public const string ReadingsTakenLineFrequency = ReadingsTakenLine + "Frequency";
    public const string ReadingsTakenLineByWhom = ReadingsTakenLine + "ByWhom";

    public const string V3LicenceNumber = nameof(V3LicenceNumber);
    public const string V3LicenceCriticality = nameof(V3LicenceCriticality);
    public const string V3LicenceHolderName = nameof(V3LicenceHolderName);
    public const string V3LicenceHolderAddress = nameof(V3LicenceHolderAddress);
    public const string V3SiteContactNumber = nameof(V3SiteContactNumber);
    public const string V3SiteContactEmail = nameof(V3SiteContactEmail);
    public const string V3SiteAddress = nameof(V3SiteAddress);

    public const string V3AssessmentType = nameof(V3AssessmentType);
    public const string V3PersonMetWith = nameof(V3PersonMetWith);
    public const string V3JobTitleOrRole = nameof(V3JobTitleOrRole);
    public const string V3AssessmentDate = nameof(V3AssessmentDate);
    public const string V3InspectionStartTime = nameof(V3InspectionStartTime);
    public const string V3InspectionEndTime = nameof(V3InspectionEndTime);
    public const string V3InspectingOfficers = nameof(V3InspectingOfficers);

    public const string V3MeterDetailsBlock = nameof(V3MeterDetailsBlock);
    public const string V3LicenceConditionsBlock = nameof(V3LicenceConditionsBlock);
    public const string V3NonCompliancesBlock = nameof(V3NonCompliancesBlock);
    public const string V3EnforcementResponse = nameof(V3EnforcementResponse);
    public const string V3ActionsBlock = nameof(V3ActionsBlock);
    public const string V3GeneralComments = nameof(V3GeneralComments);
    public const string V3DocumentTemplateVersion = nameof(V3DocumentTemplateVersion);

    // The conditions v3 assesses that v1 never printed, so they have no shared provision slot.
    public const string V3ConditionHandsOffFlow = nameof(V3ConditionHandsOffFlow);
    public const string V3ConditionCompensationOrAugmentation = nameof(V3ConditionCompensationOrAugmentation);
    public const string V3ConditionFishAndEelScreening = nameof(V3ConditionFishAndEelScreening);

}