using iTextSharp.text.pdf;
using org.pdfclown.documents.contents.objects;
using org.pdfclown.files;
using File = System.IO.File;
using Path = org.pdfclown.documents.contents.objects.Path;

namespace WRADI.Services.AbstractionLicence.Tests.IntegrationTests;

public class PdfClownTests
{
    [Fact]
    public void PdfClown_DetectTableBorders()
    {
        // Arrange
        const string filename = "wr51__sw0480192006__c9b5d652-2132-42be-9542-58f7dd894d92.pdf";
        
        // iTextSharp
        PdfReader.unethicalreading = true;
        var reader = new PdfReaderExtended($"{TestConfig.PdfFolder}/{filename}");
        reader.DecryptOnPurpose();
        
        using var memoryStream = new MemoryStream();
        var stamper = new PdfStamper(reader, memoryStream);
        stamper.Close();
        reader.Close();
        
        File.WriteAllBytes("output-wradi.pdf", memoryStream.ToArray());
        
        using var pdf = new org.pdfclown.files.File("output-wradi.pdf");
        var doc = pdf.Document;
        
        foreach (var page in doc.Pages)
        {
            // TODO, eventually swap for recursion ideally
            foreach (var content in page.Contents)
            {
                // E.g. LocalGraphicsState
                if (content is CompositeObject compositeObject)
                {
                    foreach (var subContent in compositeObject.Objects)
                    {
                        // E.g. MarkedContent
                        if (subContent is CompositeObject subCompositeObject)
                        {
                            foreach (var subSubContent in subCompositeObject.Objects)
                            {
                                // E.g. LocalGraphicsState
                                if (subSubContent is CompositeObject subSubCompositeObject)
                                {
                                    foreach (var subSubSubContent in subSubCompositeObject.Objects)
                                    {
                                        // E.g. Path
                                        if (subSubSubContent is CompositeObject subSubSubCompositeObject)
                                        {
                                            foreach (var subSubSubSubContent in subSubSubCompositeObject.Objects)
                                            {
                                                if (subSubSubSubContent is CompositeObject)
                                                {
                                                    throw new Exception(
                                                        $"Don't expect this level of iteration (4 levels deep) - {subSubSubSubContent.GetType().FullName}");
                                                }
                                                // E.g. Draw Rectangle
                                                else
                                                {
                                                    
                                                }
                                            }
                                        }
                                        else
                                        {
                                            
                                        }
                                    }
                                }
                                else
                                {
                                    
                                }
                            }
                        }
                        else
                        {
                            // TODO handle all the types we care about
                        }
                    }
                }
                else
                {
                    throw new Exception(
                        $"Don't know how to handle a top-level none composite object - {content.GetType().FullName}");
                }
            }
        }

        // Act
        // Assert
    }

    [Fact]
    public void PdfClown_ReplaceText()
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

        ContentObject? updatedContent = null;

        var textToFind = "ABSTRACT".Select(l => (byte)l).ToArray();
        
        // Seem to be limited by what characters are available or registered in the font/glyph
        var replacementText = "ABC     ".Select(l => (byte)l).ToArray();
        
        foreach (var page in doc.Pages)
        {
            var updateIndex = -1;
            var pageContentIndex = 0;
            
            foreach (var content in page.Contents)
            {
                if (content is Text text)
                {
                    foreach (var subContent in text.Objects)
                    {
                        if (subContent is not ShowText showText)
                        {
                            continue;
                        }
                        
                        var existingValue = (byte[])showText.Value[0];

                        if (!existingValue.SequenceEqual(textToFind))
                        {
                            continue;
                        }
                        
                        var updatedValues = showText.Value;
                        updatedValues.Clear();
                        updatedValues.Add(replacementText);

                        showText.Value = updatedValues;
                            
                        updateIndex = pageContentIndex;
                        updatedContent = content;
                    }
                }
                
                pageContentIndex += 1;
            }

            if (updateIndex != -1)
            {
                var contents = page.Contents;
                
                // Changing inline doesnt work, this does
                contents.RemoveAt(updateIndex);
                contents.Insert(updateIndex, updatedContent);
                
                contents.Flush();
            }
        }

        pdf.Save("Test2.pdf", SerializationModeEnum.Incremental);

        // Act

        // Assert
    }
}