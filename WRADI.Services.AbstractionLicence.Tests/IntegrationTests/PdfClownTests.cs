using iTextSharp.text.pdf;

namespace WRADI.Services.AbstractionLicence.Tests.IntegrationTests;

public class PdfClownTests
{
    [Fact]
    public void PdfClown_ReadTest()
    {
        // Arrange
        const string filename = "NE0270023036__Application - New - Issued Licence 03.03.2017 9705232.pdf";
        
        // iTextSharp
        PdfReader.unethicalreading = true;
        var reader = new PdfReaderExtended($"{TestConfig.PdfFolder}/{filename}");
        reader.DecryptOnPurpose();
        
        using var memoryStream = new MemoryStream();
        var stamper = new PdfStamper(reader, memoryStream);
        stamper.Close();
        reader.Close();
        
        File.WriteAllBytes("output4.pdf", memoryStream.ToArray());
        
        using var pdf = new org.pdfclown.files.File("output4.pdf");
        var doc = pdf.Document;

        foreach (var page in doc.Pages)
        {
            foreach (var a in page.Resources.Fonts.Keys)
            {
                Console.WriteLine(page.Resources.Fonts[a].Name);
            }
        }
        
        // Act

        // Assert
    }
}