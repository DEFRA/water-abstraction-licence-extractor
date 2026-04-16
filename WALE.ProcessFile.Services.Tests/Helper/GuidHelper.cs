using System.Security.Cryptography;
using System.Text;

namespace WALE.ProcessFile.Services.Tests.Helper;

public static class GuidHelper
{
    public static Guid GetConsistentFileIdFromFilename(string filename)
    {
        var bytes = Encoding.UTF8.GetBytes(filename);
        var hash = MD5.HashData(bytes);

        return new Guid(hash);
    }
}