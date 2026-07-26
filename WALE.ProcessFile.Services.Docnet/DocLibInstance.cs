using Docnet.Core;
using Docnet.Core.Models;
using Docnet.Core.Readers;

namespace WALE.ProcessFile.Services.Docnet;

public class DocLibInstance
{
    private static readonly DocLib Instance = DocLib.Instance;
    
    public IDocReader GetDocReader(
        Stream stream,
        PageDimensions pageDimensions)
    {
        if (stream == null)
        {
            throw new NullReferenceException(nameof(stream));
        }
        
        var bytes = GetByteArray(stream);
        return Instance.GetDocReader(bytes, pageDimensions);
    }
    
    private static byte[] GetByteArray(Stream stream)
    {
        if (stream is MemoryStream memStream)
        {
            return memStream.ToArray();
        }

        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        
        var bytes = memoryStream.ToArray();
        return bytes;
    }
}