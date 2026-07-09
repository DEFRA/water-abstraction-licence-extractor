using Docnet.Core;
using Docnet.Core.Models;
using Docnet.Core.Readers;
using WALE.ProcessFile.Core.Interfaces;

namespace WALE.ProcessFile.Services.Docnet;

public class DocLibInstance
{
    private static readonly DocLib Instance = DocLib.Instance;
    
    public async Task<IDocReader> GetDocReaderAsync(
        IFileService fileService,
        string pdfFilename,
        PageDimensions pageDimensions)
    {
        var stream = await fileService.GetFileAsStreamAsync(pdfFilename);
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