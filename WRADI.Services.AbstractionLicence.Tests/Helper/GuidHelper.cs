using System.Security.Cryptography;
using System.Text;

namespace WRADI.Services.AbstractionLicence.Tests.Helper;

public static class GuidHelper
{
    public static Guid GetConsistentFileIdFromFilename(string filename)
    {
        var bytes = Encoding.UTF8.GetBytes(filename);
        var hash = MD5.HashData(bytes);

        return new Guid(hash);
    }
}