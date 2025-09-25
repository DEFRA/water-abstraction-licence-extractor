using WALE.ProcessFile.Services.Interfaces;

namespace WALE.ProcessFile.Services.Models;

public class FunctionInputModel
{
    public LabelToMatch? label { get; set; }
    public LabelGroupResult? labelGroupResult { get; set; }
    public DocumentLine? line { get; set; }
    public DocumentLine? lineForPosition { get; set; }
    public IReadOnlyList<DocumentLine>? previousLines { get; set; }
    public IReadOnlyList<DocumentLine>? nextLines { get; set; }
    public IReadOnlyList<LabelGroupResult>? siblingMatches { get; set; }
    public List<TextAndLabel>? textBeforeAtAndAfterLabel { get; set; }
    public bool isDateLookup { get; set; }
    public bool isDateOrPurposeLookup { get; set; }
    public bool isCompanyType { get; set; }
    public bool isNumberLookup { get; set; }
    public bool isLicenceNumberLookup { get; set; }
    public int lineNumber { get; set; }
    public bool isSingleWord { get; set; }
    public bool actsLikeSingleWord { get; set; }
    public bool isUnitsLookup { get; set; }
    public bool isOcr { get; set; }
    public string? serviceName { get; set; }
    public string? labelGroupName { get; set; }
    public Dictionary<string, string>? licenceMapping { get; set; }
    public List<string>? previouslyParsedPaths { get; set; }
    public string? outputFolder { get; set; }
    public string? cacheFolder { get; set; }
    public IPdfDataExtractorService? pdfDataExtractorService { get; set; }
    
    public FunctionInputModel Clone()
    {
        return new FunctionInputModel
        {
            label = label?.Clone(),
            labelGroupResult = labelGroupResult?.Clone(),
            line = line?.Clone(),
            lineForPosition = lineForPosition?.Clone(),
            previousLines = previousLines,
            nextLines = nextLines,
            siblingMatches = siblingMatches,
            textBeforeAtAndAfterLabel = textBeforeAtAndAfterLabel,
            isDateOrPurposeLookup = isDateOrPurposeLookup,
            isDateLookup = isDateLookup,
            isCompanyType = isCompanyType,
            isNumberLookup = isNumberLookup,
            isLicenceNumberLookup = isLicenceNumberLookup,
            lineNumber = lineNumber,
            isSingleWord = isSingleWord,
            actsLikeSingleWord = actsLikeSingleWord,
            isUnitsLookup = isUnitsLookup,
            isOcr = isOcr,
            serviceName = serviceName,
            labelGroupName = labelGroupName,
            licenceMapping = licenceMapping,
            previouslyParsedPaths = previouslyParsedPaths,
            outputFolder = outputFolder,
            cacheFolder = cacheFolder,
            pdfDataExtractorService = pdfDataExtractorService
        };
    }
}