using System.Text.Json.Serialization;
using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Helpers;

namespace WALE.ProcessFile.Core.Models;

public class DocumentLineColumn
{
    public DocumentLineColumn(List<DocumentLineWord> words)
    {
        Words = words;
    }
    
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

    private List<DocumentLineWord>? _words;
    public List<DocumentLineWord> Words
    {
        get => _words!;
        set
        {
            foreach (var word in value)
            {
                if (word.Text.Contains(' '))
                {
                    throw new Exception($"Word cannot contain space ('{word.Text}')");
                }
            }
            
            _words = value;
        }
    }

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
    
    public static List<DocumentLineWord> TextToWords(
        string text,
        double? ocrConfidence,
        DocumentLineWordCoordinates? coordinates = null)
    {
        return text
            .Split(' ')
            .Select(word =>
                new DocumentLineWord(
                    word,
                    ocrConfidence,
                    coordinates ?? PositionConstants.UnknownCoordinates,
                    null))
            .ToList();
    }

    public static List<DocumentLineWord> FilterWordsFromText(List<DocumentLineWord> inputWords, string inputText)
    {
        var inputTextTrimmed = FormattingHelper.TrimFormatting(inputText, true, true);
        var inputTextWords = TextToWords(inputTextTrimmed!, null);

        var inputWordsTrimmed = FormattingHelper.TrimFormatting(inputWords.ToList());
        var inputWordsCopy = inputWordsTrimmed.ToList();
            
        foreach (var inputTextWord in inputTextWords)
        {
            var position = inputWordsCopy.FindIndex(lw => lw.Text == inputTextWord.Text);
                
            if (position == -1)
            {
                throw new Exception($"Words doesn't contain '{inputTextWord.Text}' from the input text '{inputTextTrimmed}'");
            }

            inputWordsCopy = inputWordsCopy.Slice(position, inputWordsCopy.Count - position);
        }

        var outputWords = new List<DocumentLineWord>();
            
        foreach (var inputWord in inputWordsTrimmed)
        {
            var exists = inputTextWords.Any(ots => ots.Text == inputWord.Text);

            if (!exists)
            {
                continue;
            }
                
            outputWords.Add(inputWord);
        }

        var outputWordsText = string.Join(' ', outputWords.Select(w => w.Text));
        
        System.Diagnostics.Debug.Assert(inputTextTrimmed == outputWordsText, $"Words are different between;\n\n(Input)  - {inputText}\n(Output) - {outputWordsText} ");
        return outputWords;
    }
}