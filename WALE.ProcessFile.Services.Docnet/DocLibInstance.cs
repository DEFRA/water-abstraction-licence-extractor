using Docnet.Core;
using Docnet.Core.Models;
using Docnet.Core.Readers;

namespace WALE.ProcessFile.Services.Docnet;

public class DocLibInstance
{
    private static readonly DocLib Instance = DocLib.Instance;
    
    public IDocReader GetDocReader(string pdfFilePath, PageDimensions pageDimensions)
    {
        return Instance.GetDocReader(pdfFilePath, pageDimensions);
    }
}