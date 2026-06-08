using UglyToad.PdfPig.Content;
using WALE.ProcessFile.Core.Interfaces;

namespace WALE.ProcessFile.Services.PdfPig;

public class PdfPigInternalPdfImage(IPdfImage image) : IInternalPdfImage
{
    public bool TryGetBytesAsMemory(out Memory<byte> memory)
    {
        try
        {
            return image.TryGetBytesAsMemory(out memory);
        }
        catch (EndOfStreamException)
        {
            // Some error with JBig2 on a small subset of images (907bb5a8-b735-440a-a9e9-0d49872d0ddd P4 I2 has it)
            
            memory = null;
            return false;
        }
    }

    public bool TryGetPng(out byte[]? bytes)
    {
        try
        {
            return image.TryGetPng(out bytes);
        }
        catch (EndOfStreamException)
        {
            // Some error with JBig2 on a small subset of images (907bb5a8-b735-440a-a9e9-0d49872d0ddd P4 I2 has it)
            
            bytes = null;
            return false;
        }
    }

    public Span<byte> RawBytes => image.RawBytes;
}