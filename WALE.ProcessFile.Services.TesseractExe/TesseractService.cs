using WALE.ProcessFile.Services.Tesseract;

namespace WALE.ProcessFile.Services.TesseractExe;

public static class TesseractService
{
    public static InternalTesseractOcrDataExtractorService? Instance { get; set; }
}