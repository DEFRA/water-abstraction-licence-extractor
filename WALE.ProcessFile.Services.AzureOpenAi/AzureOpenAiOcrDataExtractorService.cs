using System.ClientModel;
using Azure.AI.OpenAI;
using Microsoft.ML.Tokenizers;
using OpenAI.Chat;
using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Services.AzureOpenAi;

public class AzureOpenAiOcrDataExtractorService(
    string endpoint,
    string key,
    string modelName,
    string deploymentName,
    ICacheService cacheService)
    : IOcrDataExtractorService, IDisposable
{
    public bool HasDirectCost => true;
    public string Name => "AzureOpenAiOcr";
    
    public async Task<IReadOnlyList<DocumentLine>>
        GetTextLinesFromImageAsync(
            string imageReference,
            string pdfFilepath,
            int pageNumber,
            int imageNumber,
            PdfDocument pdfDocument,
            int processRunId,
            string noOcrServiceName)
    {
        var isPageScreenshot = OcrHelper.IsPageScreenshot(imageReference, pageNumber);
        
        string? response;
        var request = new OcrServiceImageTextCacheRequest
        {
            PageNumber = pageNumber,
            ImageNumber = imageNumber,
            Filepath = pdfFilepath,
            OcrServiceName = Name,
            ProcessRunId = processRunId
        };
        
        var cacheFileText = isPageScreenshot
            ? await cacheService.GetOcrScreenshotTextAsync(request)
            : await cacheService.GetOcrImageTextAsync(request);
        
        if (pdfDocument.FromCache && !string.IsNullOrEmpty(cacheFileText))
        {
            response = cacheFileText;
        }
        else
        {
            var azureClient = new AzureOpenAIClient(
                new Uri(endpoint),
                new ApiKeyCredential(key));
        
            var chatClient = azureClient.GetChatClient(deploymentName);
        
            var userPrompts = new List<ChatMessageContentPart>
            {
                ChatMessageContentPart.CreateTextPart(
                    "Please fetch me this whole document as text. Do "
                    + "not change it at all. Give me only this - do not add any follow up questions or advice. Do not add markdown."
                )
            };
        
            var imagePrompt = await GetImagePromptAsync(
                pageNumber,
                imageNumber,
                pdfFilepath,
                imageReference);
            
            userPrompts.Add(imagePrompt);
            
            response = await GetTextResponseAsync(
                chatClient,
                [
                    "You are an AI assistant that extracts a the text from documents"
                    + " and returns it as is. Return only this text, with no other instructions or text. DO NOT give a description of the image" ],
                userPrompts);

            if (isPageScreenshot)
            {
                await cacheService.SaveOcrScreenshotTextAsync(request, response!);                
            }
            else
            {
                await cacheService.SaveOcrImageTextAsync(request, response!);                
            }
        }

        if (string.IsNullOrEmpty(response)
            || response.Contains("I am unable", StringComparison.InvariantCultureIgnoreCase)
            || response.Contains("I am not able", StringComparison.InvariantCultureIgnoreCase)
            || response.StartsWith("I'm sorry", StringComparison.InvariantCultureIgnoreCase))
        {
            return new List<DocumentLine>();
        }
        
        return ToPageLines(response.Trim(), pageNumber);
    }
    
    private static List<DocumentLine> ToPageLines(string text, int pageNumber)
    {
        var lineNumber = 0;

        var unknownCoords = new DocumentLineWordCoordinates(
            PositionConstants.UnknownCoordinate,
            PositionConstants.UnknownCoordinate,
            PositionConstants.UnknownCoordinate,
            PositionConstants.UnknownCoordinate);
        
        return text
            .Replace("\r", string.Empty)
            .Split('\n')
            .Select(lineText =>
            {
                var documentLine = new DocumentLine
                {
                    LineNumber = lineNumber++,
                    PageNumber = pageNumber
                };

                var column = new DocumentLineColumn(
                    lineText,
                    lineText
                        .Split(' ')
                        .Select(wordText => new DocumentLineWord(wordText, -1, unknownCoords, null)).ToList());
                
                documentLine.Columns.Add(column);
                
                return documentLine;
            })
            .ToList();
    }
    
    async Task<ChatMessageContentPart> GetImagePromptAsync(int pageNumber, int imageNumber, string pdfFilepath, string imageReference)
    {
        var imageBytes = await cacheService.GetImageBytesAsync(new OcrServiceImageDataCacheRequest
        {
            PageNumber = pageNumber,
            ImageNumber = imageNumber,
            Filepath = pdfFilepath,
            NoOcrServiceName = GeneralConstants.PdfPigDataExtractorServiceName,
            Extension = FileHelper.GetImageExtension(imageReference)
        });
        
        if (imageBytes == null)
        {
            throw new Exception("Image was not found");
        }
        
        return ChatMessageContentPart.CreateImagePart(
            BinaryData.FromBytes(imageBytes),
            "image/jpeg",
            ChatImageDetailLevel.Auto);
    }
    
    private async Task<string?> GetTextResponseAsync(
        ChatClient chatClient,
        List<ChatMessageContentPart> systemPrompts,
        List<ChatMessageContentPart> userPrompts)
    {
        var chatResponse = await chatClient.CompleteChatAsync(
            new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompts),
                new UserChatMessage(userPrompts)
            },
            new ChatCompletionOptions
            {
                MaxOutputTokenCount = GetMaxTokens(modelName, systemPrompts, userPrompts),
                ResponseFormat = ChatResponseFormat.CreateTextFormat()
            });

        return chatResponse.Value?.Content.FirstOrDefault()?.Text;
    }
    
    private static int GetMaxTokens(
        string modelName,
        List<ChatMessageContentPart> systemPrompts,
        List<ChatMessageContentPart> userPrompts)
    {
        var tokenizer = TiktokenTokenizer.CreateForModel(modelName);
        var inputTokenCount = 0;
    
        foreach (var systemPrompt in systemPrompts)
        {
            inputTokenCount += tokenizer.CountTokens(systemPrompt.Text);
        }
    
        foreach (var prompt in userPrompts)
        {
            if (prompt.Kind == ChatMessageContentPartKind.Image)
            {
                inputTokenCount += 1120; // Guesstimate, but works out about okay for image I used (over by about 30)
                continue;
            }
                
            inputTokenCount += tokenizer.CountTokens(prompt.Text);
        }

        const int maxTokensAllowedForModel = 16_000;
        return maxTokensAllowedForModel - inputTokenCount;
    }
    
    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}