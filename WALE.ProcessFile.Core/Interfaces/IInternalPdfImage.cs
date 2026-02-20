namespace WALE.ProcessFile.Core.Interfaces;
public interface IInternalPdfImage
{
    /// <summary>
    /// Get the decoded memory of the image if applicable. For JPEG images and some other types the
    /// <see cref="RawMemory"/> should be used directly.
    /// </summary>
    bool TryGetBytesAsMemory(out Memory<byte> memory);

    /// <summary>
    /// Try to convert the image to PNG. Doesn't support conversion of JPG to PNG.
    /// </summary>
    bool TryGetPng(out byte[]? bytes);
    
    /// <summary>
    /// The encoded memory span of the image with all filters still applied.
    /// </summary>
    Span<byte> RawBytes { get; }
}