using WALE.ProcessFile.Services.Interfaces;

namespace WALE.ProcessFile.Services.Models;

public class FunctionInputModel
{
    public LabelToMatch label { get; set; }
    public LabelGroupResult labelGroupResult { get; set; }
    public DocumentLine line { get; set; }
    public IReadOnlyList<DocumentLine> previousLines { get; set; }
    public IReadOnlyList<DocumentLine> nextLines { get; set; }
    public IReadOnlyList<LabelGroupResult> siblingMatches { get; set; }
    public List<(string? Text, LabelToMatch Label)> textBeforeAndAfterLabel { get; set; }
    public bool isDateOrPurposeLookup { get; set; }
    public bool isCompanyType { get; set; }
    public bool isNumberLookup { get; set; }
    public  bool isLicenceNumberLookup { get; set; }
    public  int lineNumber { get; set; }
    public bool isSingleWord { get; set; }
    public bool actsLikeSingleWord { get; set; }
    public bool isUnitsLookup { get; set; }
    public  bool isOcr { get; set; }
    public string? serviceName { get; set; }
    public string labelGroupName { get; set; }
    public Dictionary<string, string> licenceMapping { get; set; }
    public  List<string> previouslyParsedPaths { get; set; }
    public  string outputFolder { get; set; }
    public bool useCache { get; set; }
    public IPdfDataExtractorService pdfDataExtractorService { get; set; }
}