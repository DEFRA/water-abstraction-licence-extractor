using System.IO.Compression;
using System.Text;

namespace WALE.ProcessFile.Core.Helpers;

public static class CompressionHeper
{
    public static async Task<byte[]> ToGzipAsync(this string value, CompressionLevel level = CompressionLevel.Fastest)
    {
        var bytes = Encoding.Unicode.GetBytes(value);
        
        using var memoryStream = new MemoryStream();
        await using (var gzipStream = new GZipStream(memoryStream, level))
        {
            gzipStream.Write(bytes, 0, bytes.Length);
        }
        
        return memoryStream.ToArray();
    }
    
    public static async Task<string> FromGzipAsync(this byte[] bytes)
    {
        using var memoryStream = new MemoryStream(bytes);
        using var outputStream = new MemoryStream();
        await using (var decompressStream = new GZipStream(memoryStream, CompressionMode.Decompress))
        {
            await decompressStream.CopyToAsync(outputStream);
        }
        
        var outputBytes = outputStream.ToArray();
        return Encoding.Unicode.GetString(outputBytes);
    }
}