namespace WRADI.DocumentType.WrInspectionReport.Enums;

/// Which date a report is listed under, worst last.
public enum WrInspectionReportDateSource
{
    /// No date from any source, which is itself a finding.
    None,

    /// The document's own inspection date.
    Extracted,

    /// The form's metadata date, used when the inspection date did not match.
    Metadata,

    /// When DMS recorded the document, which is a filing date, not an inspection date.
    DmsDocument,

    /// When the file was uploaded to DMS. Present for every row, so it is the last resort.
    DmsUpload
}
