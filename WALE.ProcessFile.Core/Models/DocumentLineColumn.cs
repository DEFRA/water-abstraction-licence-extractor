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
        // TODO replace with a source generator
        
        return new DocumentLineColumn(Words.Select(w => w.Clone()).ToList());
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

    public static List<DocumentLineWord> FilterWordsFromText(
        List<DocumentLineWord> inputWords,
        string inputText,
        bool throwIfMissing = false)
    {
        var inputTextTrimmed = FormattingHelper.TrimFormatting(inputText, true, true);
        
        if (string.IsNullOrEmpty(inputTextTrimmed))
        {
            return [];
        }
        
        var inputTextWords = TextToWords(inputText, null);
        var inputWordsTrimmed = FormattingHelper.TrimFormatting(inputWords.ToList());

        // The following 2 should reflect each other
        var inputWordsCopy = inputWords.ToList();
        var inputWordsTrimmedCopy = inputWordsTrimmed.ToList();
        
        var outputWords = new List<DocumentLineWord>();
        
        foreach (var inputTextWord in inputTextWords)
        {
            var isFirstWord = inputTextWords[0] == inputTextWord;
            var isLastWord = inputTextWords.Last() == inputTextWord;

            var inputTextWordTrimmedText = FormattingHelper.TrimFormatting(
                inputTextWord.Text,
                isFirstWord,
                isLastWord);

            if (string.IsNullOrEmpty(inputTextWordTrimmedText))
            {
                continue;
            }

            var position = inputWordsTrimmedCopy.FindIndex(lw =>
                lw.Text.Contains(inputTextWordTrimmedText, StringComparison.OrdinalIgnoreCase));
                
            if (position == -1)
            {
                if (!throwIfMissing)
                {
                    return outputWords;
                }
                
                var inputWordsForDisplay = string.Join(' ', inputWords.Select(iw => iw.Text));
                throw new Exception($"Words don't contain input text '{inputTextWordTrimmedText}';\n\nWords - '{inputWordsForDisplay}'\nText  - '{inputText}'");
            }

            var startPos = inputWordsCopy[position].Text.IndexOf(inputTextWord.Text, StringComparison.OrdinalIgnoreCase);

            if (startPos == -1)
            {
                position = inputWordsCopy.FindIndex(
                    lw => lw.Text.StartsWith(inputTextWord.Text, StringComparison.OrdinalIgnoreCase)
                          || lw.Text.EndsWith(inputTextWord.Text, StringComparison.OrdinalIgnoreCase));

                startPos = inputWordsCopy[position].Text.IndexOf(inputTextWord.Text, StringComparison.OrdinalIgnoreCase);
                
                if (startPos == -1)
                {
                    throw new Exception("Issue with difference between punctuation and none-punctuation versions");
                }
            }
            
            var substring = inputWordsCopy[position].Text.Substring(startPos, inputTextWord.Text.Length);

            var inputWordsCopyClone = inputWordsCopy[position].Clone();
            inputWordsCopyClone.Text = substring;
            
            outputWords.Add(inputWordsCopyClone);
            
            inputWordsCopy = inputWordsCopy.Slice(position + 1, inputWordsCopy.Count - position - 1);
            inputWordsTrimmedCopy = inputWordsTrimmedCopy.Slice(position + 1, inputWordsTrimmedCopy.Count - position - 1);
        }

        var outputWordsText = string.Join(' ', outputWords.Select(w => w.Text));
        var outputWordsTextTrimmed = FormattingHelper.TrimFormatting(outputWordsText, true, true);

        if (throwIfMissing)
        {
            System.Diagnostics.Debug.Assert(
                inputTextTrimmed.Equals(outputWordsTextTrimmed, StringComparison.OrdinalIgnoreCase),
                $"Words are different between;\n\n(Input)  - {inputText}\n(Output) - {outputWordsTextTrimmed}");
        }

        return outputWords;
    }
}