using UglyToad.PdfPig.Content;
using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Services.PdfPig;

public class PdfPigInternalPdfImage(IPdfImage image) : IInternalPdfImage
{
    public bool TryGetBytesAsMemory(out Memory<byte> memory)
    {
        return image.TryGetBytesAsMemory(out memory);
    }

    public bool TryGetPng(out byte[]? bytes)
    {
        return image.TryGetPng(out bytes);
    }

    public Span<byte> RawBytes => image.RawBytes;
}