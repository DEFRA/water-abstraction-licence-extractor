using WALE.ProcessFile.Services.Enums;
using WALE.ProcessFile.Services.Models;
using static WALE.ProcessFile.Services.Helpers.DataHelpers;

namespace WALE.ProcessFile.Services.Methods;

public static class Split
{
    private const int UnknownLinesTotal = -1;
    
    public static async Task<List<LabelGroupResult>> FunctionAsync(FunctionInputModel request)
    {
        if (request.label.Text == null || request.label.Text.Count == 0)
        {
            throw new Exception("Incorrect configuration - if position is Split, Text must be set");
        }

        var lineContainsLabel = LineContainsLabel(
            request.line,
            request.label.Text,
            LabelPosition.Split,
            UnknownLinesTotal,
            int.MaxValue,
            out _);

        var sub1Result = request.labelGroupResult.Clone();
        var sub1ResultText  = request.previousLines.Reverse().ToList();

        if (!lineContainsLabel)
        {
            sub1ResultText.Add(request.line);
        }
        
        var sub2Result = request.labelGroupResult.Clone();
        var sub2ResultText = request.nextLines.ToList();

        if (lineContainsLabel)
        {
            if (sub1ResultText.Count == 0 && sub2ResultText.Count == 0)
            {
                var splitter = string.Join(' ', request.label.Text);
                var parts = request.line.Text.Split(splitter);

                parts[0] = parts[0].Trim();
                parts[1] = parts[1].Trim();
                
                var lineWords1 = parts[0]
                    .Split(' ')
                    .Select(t => new DocumentLineWord(t, null, []))
                    .ToList();

                var lineWords2 = parts[1]
                    .Split(' ')
                    .Select(t => new DocumentLineWord(t, null, []))
                    .ToList();                                
                
                sub1ResultText = [new DocumentLine(parts[0], request.lineNumber, request.line.PageNumber, lineWords1)];
                sub2ResultText = [new DocumentLine(parts[1], request.lineNumber, request.line.PageNumber, lineWords2)];
            }
            else
            {
                sub2ResultText.Insert(0, request.line);

                if (sub1ResultText.Count > 0)
                {
                    var lastLine = sub1ResultText.Last();
                    sub2ResultText.Insert(0, lastLine);

                    sub1ResultText.Remove(lastLine);
                }
            }
        }

        sub1ResultText = RemoveMultipleBlankLines(sub1ResultText);
        sub2ResultText = RemoveMultipleBlankLines(sub2ResultText);
        
        sub1Result.Text = sub1ResultText;
        sub2Result.Text = sub2ResultText;

        var results = new List<LabelGroupResult>
        {
            sub1Result
        };

        if (sub2Result.Text.Count > 0)
        {
            results.Add(sub2Result);
        }

        foreach (var result in results)
        {
            var subResults = await request.pdfDataExtractorService.ProcessSubLabelsAsync(
                request.label,
                result.Text!,
                request.isOcr,
                request.serviceName,
                request.labelGroupName,
                request.licenceMapping,
                request. previouslyParsedPaths,
                request.outputFolder,
                request.useCache);
        
            if (request.label.MinimumSubMatches.HasValue && request.label.MinimumSubMatches.Value > subResults.Count)
            {
                return [];
            }

            result.SubResults = subResults;
        }
        
        return results;
    }
}