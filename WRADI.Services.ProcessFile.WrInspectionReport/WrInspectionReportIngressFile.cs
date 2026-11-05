namespace WRADI.Services.ProcessFile.WrInspectionReport;

/// <summary>
/// How a WR51 document is named in the shared ingress bucket: "wr51__{permit}__{fileId}.pdf",
/// where an abstraction licence file is "{permit}__{fileId}.pdf". Anything synthesising a key
/// from a discovery record must add the prefix or it asks S3 for an object that does not exist.
/// </summary>
public static class WrInspectionReportIngressFile
{
    public const string Prefix = "wr51__";

    /// <summary>
    /// The ingress key for a discovered document. Lowercased because the bucket is keyed in
    /// lowercase.
    /// </summary>
    public static string BuildFileName(string permitNumber, string fileId) =>
        $"{Prefix}{permitNumber.ToLower()}__{fileId.ToLower()}.pdf";
}
