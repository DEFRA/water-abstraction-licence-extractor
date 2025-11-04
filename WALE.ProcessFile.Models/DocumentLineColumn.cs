using WALE.ProcessFile.Models.Constants;

namespace WALE.ProcessFile.Models;

public class DocumentLineColumn(string text, List<DocumentLineWord> words)
{
    public DocumentLineColumn(string text) : this(text, TextToWords(text)) { }
    
    public DocumentLineColumn() : this(string.Empty, []) { }

    public string Text { get; set; } = text;    
    
    public List<DocumentLineWord> Words { get; set; } = words;

    public double? OcrConfidence
    {
        get
        {
            var totalConfidence = 0.0;
            
            foreach (var word in Words)
            {
                if (word.OcrConfidence == null)
                {
                    continue;
                }
                
                totalConfidence += word.OcrConfidence.Value;
            }

            var averageConfidence = totalConfidence / Words.Count;
            return averageConfidence;
        }
    }

    public DocumentLineColumn Clone()
    {
        return new DocumentLineColumn(Text, Words.ToList());
    }

    public DocumentLine AsDocumentLine(DocumentLine line)
    {
        return new DocumentLine
        {
            LineNumber = line.LineNumber,
            PageNumber = line.PageNumber,
            Columns = [this]
        };
    }
    
    private static List<DocumentLineWord> TextToWords(string text)
    {
        return text
            .Split(' ')
            .Select(word =>
                new DocumentLineWord(word, null, PositionConstants.UnknownCoordinates))
            .ToList();
    }
}