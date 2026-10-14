namespace WALE.ProcessFile.Core.Constants;

/// The document types the pipeline dispatches on. Used for ProcessRun.DocumentType, the
/// FileProcessSingleRequest, and dms_extract.extract_type, which must agree.
public static class DocumentTypes
{
    public const string AbstractionLicence = "AbstractionLicence";

    public const string WrInspectionReport = "WrInspectionReport";
}
