using System.Security.Cryptography;
using System.Text;

namespace WRADI.Services.WrInspectionReport.Tests.Helper;

/// <summary>
/// Mirrors WALE.ProcessFile.Services.Tests/Helper/GuidHelper.cs - kept as a small local copy
/// rather than a cross-project reference. Used for the WR51 "dummy" fixtures, whose
/// filenames have no real DMS GUID (WR51__&lt;licence&gt;__dummy.pdf) for FileHelper.ExtractFileId
/// to parse.
/// </summary>
public static class GuidHelper
{
    public static Guid GetConsistentFileIdFromFilename(string filename)
    {
        var bytes = Encoding.UTF8.GetBytes(filename);
        var hash = MD5.HashData(bytes);

        return new Guid(hash);
    }
}
