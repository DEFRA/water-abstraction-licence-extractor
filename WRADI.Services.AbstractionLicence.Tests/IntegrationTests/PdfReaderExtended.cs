using iTextSharp.text.pdf;

namespace WRADI.Services.AbstractionLicence.Tests.IntegrationTests;

internal class PdfReaderExtended(string filename) : PdfReader(filename)
{
    public void DecryptOnPurpose()
    {
        encrypted = false;
    }
}