namespace WALE.ProcessFile.Services.Models;

public class DocumentLineColumn(List<DocumentLineWord> words)
{
    public DocumentLineColumn() : this([]) { }

    public List<DocumentLineWord> Words { get; set; } = words;

    public DocumentLineColumn Clone()
    {
        return new DocumentLineColumn(Words.ToList());
    }
}