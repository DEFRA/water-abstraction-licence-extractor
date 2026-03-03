using System.Text.Json.Serialization;
using WALE.ProcessFile.Core.Constants;

namespace WALE.ProcessFile.Core.Models;

public class DocumentLineColumn(List<DocumentLineWord> words)
{
    public DocumentLineColumn() : this([]) { }

    [JsonIgnore]
    public string Text
    {
        get
        {
            return Words.Count == 0 ?
                string.Empty
                : string.Join(' ', Words.Select(column => column.Text));
        }
    }
    
    public List<DocumentLineWord> Words { get; set; } = words;

    public double? OcrConfidence
    {
        get
        {
            var totalConfidence = 0.0;
            var anyHaveOcrConfidence = false;
            
            foreach (var word in Words)
            {
                if (word.OcrConfidence == null)
                {
                    continue;
                }
                
                totalConfidence += word.OcrConfidence.Value;
                anyHaveOcrConfidence = true;
            }

            if (!anyHaveOcrConfidence)
            {
                return null;
            }
            
            var averageConfidence = totalConfidence / Words.Count;
            return averageConfidence;
        }
    }
    
    public DocumentLineColumn Clone()
    {
        return new DocumentLineColumn(Words.ToList());
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
    
    public static List<DocumentLineWord> TextToWords(string text, double? ocrConfidence)
    {
        return text
            .Split(' ')
            .Select(word =>
                new DocumentLineWord(
                    word,
                    ocrConfidence,
                    PositionConstants.UnknownCoordinates,
                    null))
            .ToList();
    }

    public static List<DocumentLineWord> FilterWordsFromText(List<DocumentLineWord> inputWords, string inputText)
    {
        var inputTextWords = TextToWords(inputText, null);
        var inputWordsCopy = inputWords.ToList();
            
        foreach (var inputTextWord in inputTextWords)
        {
            var position = inputWordsCopy.FindIndex(lw => lw.Text == inputTextWord.Text);
                
            if (position == -1)
            {
                throw new Exception($"Words doesn't contain '{inputTextWord}'");
            }

            inputWordsCopy = inputWordsCopy.Slice(position, inputWordsCopy.Count - position);
        }

        var outputWords = new List<DocumentLineWord>();
            
        foreach (var inputWord in inputWords)
        {
            var exists = inputTextWords.Any(ots => ots.Text == inputWord.Text);

            if (!exists)
            {
                continue;
            }
                
            outputWords.Add(inputWord);
        }

        return outputWords;
    }
}