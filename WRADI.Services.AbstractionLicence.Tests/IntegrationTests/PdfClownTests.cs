using iTextSharp.text.pdf;
using KillerPdf.Engine.Authoring;
using org.pdfclown.documents.contents.objects;
using org.pdfclown.files;
using org.pdfclown.util.collections.generic;
using File = System.IO.File;
using Path = org.pdfclown.documents.contents.objects.Path;

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

        ContentObject? updatedContentObject = null;
        var removePos = -1;
        
        foreach (var page in doc.Pages)
        {
            var idx = 0;
            
            foreach (var content in page.Contents)
            {
                if (content is Text text)
                {
                    foreach (var subContent in text.Objects)
                    {
                        if (subContent is ShowSimpleText showSimpleText)
                        {
                            var existingValue = (byte[])showSimpleText.Value[0];

                            if (existingValue.Length == 8 && existingValue[0] == 65)
                            {
                                // Seem to be limited by what characters are available or registered in the font/glyph
                                existingValue[0] = (byte)'A';
                                existingValue[1] = (byte)'B';
                                existingValue[2] = (byte)'C';
                                existingValue[3] = (byte)' ';
                                existingValue[4] = (byte)' ';
                                existingValue[5] = (byte)' ';
                                existingValue[6] = (byte)' ';
                                existingValue[7] = (byte)' ';                                

                                removePos = idx;
                                updatedContentObject = content;
                            }
                        }
                        
                        /*if (subContent is ShowText showText)
                        {
                           
                        }*/
                    }
                }
                
                /*if (content is Path path)
                {
                    foreach (var subContent in path.Objects)
                    {
                        if (subContent is MarkedContent markedContent)
                        {
                            
                        }
                    }
                }*/

                idx += 1;
            }

            if (page.Index == 0)
            {
                var contents = page.Contents;
                contents.RemoveAt(removePos);
                contents.Insert(removePos, updatedContentObject);
                contents.Flush();
            }
        }

        pdf.Save("Test1.pdf", SerializationModeEnum.Standard);

        // Act

        // Assert
    }
}