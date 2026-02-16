using System.IO.Compression;

namespace WALE.ProcessFile.Core.Helpers;

public static class ImageHelper
{
    public static byte[] Deflate(byte[] input)
    {
        var cutInput = new byte[input.Length - 2];
        Array.Copy(input, 2, cutInput, 0, cutInput.Length);

        var stream = new MemoryStream();

        using var compressStream = new MemoryStream(cutInput);
        using var decompressor = new DeflateStream(compressStream, CompressionMode.Decompress);
        
        decompressor.CopyTo(stream);
        return stream.ToArray();
    }
}