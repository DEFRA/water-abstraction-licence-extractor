using System.ClientModel;
using System.Collections;
using System.Globalization;
using System.Text.Json;
using Azure.AI.OpenAI;
using CsvHelper;
using Microsoft.ML.Tokenizers;
using OpenAI.Chat;
using PDFtoImage;
using SkiaSharp;
using WALE.ProcessFile.Services.Configuration;
using WALE.ProcessFile.Services.Converters;
using WALE.ProcessFile.Services.Helpers;
using WALE.ProcessFile.Services.Interfaces;
using WALE.ProcessFile.Services.Models;
using WALE.ProcessFile.Services.Models.OutputSchema;
using WALE.ProcessFile.Services.Models.OutputSchema.PromptSpecific;
using WALE.ProcessFile.Services.Services;
using WALE.ProcessFile.Services.Services.PdfPig;
using WALE.Tools;

//const string workflow = "GenerateCsvForTesting";
const string workflow = "TestsForAiPrompts";

switch (workflow)
{
    case "GenerateCsvForTesting":
        await GenerateCsvForTestingAsync();
        break;
    case "TestsForAiPrompts":
        await TestsForAiPromptsAsync();
        break;
}

return;

async Task TestsForAiPromptsAsync()
{
    var pdfFilenames = new List<string>
    {
        "2-26-32-126 6937559.PDF",
        /*"2-27-29-012 7003124.PDF",
        "Application - New - Licence Issued 30092021.pdf",
        "Application Formal Variation Issued Licence 07032023 (1).pdf",
        "Application Formal Variation Issued Licence 07032023.pdf",
        "Application Minor Variation Issued Licence 03.10.24.pdf"*/
    };

    foreach (var pdfFilename in pdfFilenames)
    {
        try
        {
            var pdfFile = await File.ReadAllBytesAsync(KeyConfig.PdfFolder + pdfFilename);

            #pragma warning disable CA1416
            var pageImages = Conversion.ToImages(pdfFile).ToList();
            #pragma warning restore CA1416

            var totalPageCount = pageImages.Count;

            var maxImageCount = 25.0;
            var maxSize = (int)Math.Ceiling(totalPageCount / maxImageCount);

            var pageImageGroups = new List<List<SKBitmap>>();

            for (var i = 0; i < totalPageCount; i += maxSize)
            {
                var pageImageGroup = pageImages
                    .Skip(i)
                    .Take(maxSize)
                    .ToList();

                pageImageGroups.Add(pageImageGroup);
            }
            
            var azureClient = new AzureOpenAIClient(
                new Uri(KeyConfig.OpenAiEndpoint),
                new ApiKeyCredential(KeyConfig.OpenAiKey));
            
            var modelName = "gpt-4o"; // gpt-4o-mini gets stuck it seems
            var deploymentName = "gpt-4o"; // gpt-4o-mini gets stuck it seems

            var chatClient = azureClient.GetChatClient(deploymentName);

            var imagePrompts = await GetImagePromptsAsync(
                pdfFilename,
                pageImageGroups);
            
            Console.WriteLine("Looking up document text");
            
            var allDocumentText = await GetDocumentTextAsync(
                chatClient,
                modelName,
                imagePrompts);

            Console.WriteLine($"Found document text - {allDocumentText?.Length} lines");
            
            if (string.IsNullOrEmpty(allDocumentText))
            {
                Console.WriteLine("Moving on to the next record");
                break;
            }
            
            /*Console.WriteLine("Looking up abstraction limits section");
            
            var abstractionLimitsSectionText = await GetAbstractionLimitsTextAsync(
                chatClient,
                modelName,
                allDocumentText);

            Console.WriteLine("Found abstraction limits section");
            
            if (string.IsNullOrEmpty(abstractionLimitsSectionText))
            {
                Console.WriteLine("Moving on to the next record");
                break;
            }
            
            Console.WriteLine("Looking up points");
            var points = await GetPointsAsync(chatClient, modelName, allDocumentText);
            Console.WriteLine($"Found {points.Length} points");
            
            Console.WriteLine("Looking up purposes");
            var purposes = await GetPurposesAsync(chatClient, modelName, allDocumentText);
            Console.WriteLine($"Found {purposes.Length} purposes");
            
            // TODO individual limits and aggregate limits should take into account points and purposes
            
            Console.WriteLine("Looking up individual limits");
            
            var individualLimits = await GetIndividualAbstractionLimitsAsync(
                chatClient,
                modelName,
                abstractionLimitsSectionText);
            
            Console.WriteLine($"Found {individualLimits.Length} individual abstraction limit(s)");
            
            Console.WriteLine("Looking up aggregate limits");
            
            var aggregateLimits = await GetAggregateLimitsAsync(
                chatClient,
                modelName,
                abstractionLimitsSectionText);
            
            Console.WriteLine($"Found {aggregateLimits.Length} aggregate limit(s)");
            
            Console.WriteLine("Looking up licence version");
            var licenceVersion = await GetLicenceVersionAsync(chatClient, modelName, allDocumentText);
            Console.WriteLine("Found licence version");
            
            Console.WriteLine("Looking up general licence data");
            var baseLicenceData = await GetBaseLicenceDataAsync(chatClient, modelName, allDocumentText);
            Console.WriteLine("Found general licence data");*/
            
            Console.WriteLine("Looking up means of abstraction");
            var meansOfAbstraction = await GetMeansOfAbstractionAsync(chatClient, modelName, allDocumentText);
            Console.WriteLine($"Found {meansOfAbstraction.Length} means of abstraction");
            
            Console.WriteLine("Looking up periods of abstraction");
            var periodsOfAbstraction = await GetPeriodsOfAbstractionAsync(chatClient, modelName, allDocumentText);
            Console.WriteLine($"Found {periodsOfAbstraction.Length} periods of abstraction");
            
            /*var schema = new Licence
            {
                Filename = pdfFilename,
                LicenceVersion = licenceVersion,
                Points = points,
                Purposes = purposes,
                AbstractionLimits = new AbstractionLimits
                {
                    Individual = individualLimits,
                    Aggregates = aggregateLimits
                },
                LicenceNumber = baseLicenceData.LicenceNumber,
                MeansOfAbstraction = meansOfAbstraction,
                DefinitionOfYear = baseLicenceData.DefinitionOfYear,
                PeriodsOfAbstraction = periodsOfAbstraction
            };
            
            var filenameNoExtension = pdfFilename.Split('.').First();
            var filenameNoSpacesOrDashes = filenameNoExtension
                .Replace("-", string.Empty)
                .Replace(" ", string.Empty);

            var json = JsonSerializer.Serialize(schema, JsonHelper.GetSerializer());
            var outputJs = $"window.aiData['{filenameNoSpacesOrDashes}'] = {json};";
            
            await File.WriteAllTextAsync(filenameNoExtension + ".js", outputJs);
            Console.Write(outputJs);*/    
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
}

async Task<PurposeOfAbstraction[]> GetPurposesAsync(
    ChatClient chatClient,
    string modelName,
    string allDocumentText)
{
    var userPrompts = new List<ChatMessageContentPart>
    {
        ChatMessageContentPart.CreateTextPart(
            "If a value is not present, provide null. " +
            "This array relates to 'purposes of abstraction' or similarly titled in a specific section of the the document - there may be multiple of these. " +
            "Only populate the 'pointIds' property value when the purpose text explicitly mentions at least one point - if there are no points mentioned in the purpose, 'pointIds' value should be '[]'. As an example, 'Public water supply' DOES NOT contain a point. " +
            $"Use the following structure:\n\n[{PurposeOfAbstractionArrayWrapped.GetSchemaForPrompt()}]")
    };

    var systemPrompts = new List<ChatMessageContentPart>
    {
        "You are an AI assistant that extracts data from documents"
        + " and returns them as structured JSON objects. Do not return as a code block. Extract the data from this licence.  Here is the licence to look at;"
        + Environment.NewLine
        + Environment.NewLine
        + allDocumentText
    };
            
    var textResponse = await GetTextResponseAsync(chatClient, modelName, systemPrompts, userPrompts);
    if (textResponse == null) throw new Exception("Some error occured");
            
    var response = JsonSerializer.Deserialize<PurposeOfAbstractionArrayWrapped>(textResponse, JsonHelper.GetSerializer())!;
    return response.Data;
}

async Task<PointOfAbstraction[]> GetPointsAsync(
    ChatClient chatClient,
    string modelName,
    string allDocumentText)
{
    var userPrompts = new List<ChatMessageContentPart>
    {
        ChatMessageContentPart.CreateTextPart(
            "If a value is not present, provide null. " +
            "This array relates to 'points of abstraction' or similarly titled in a specific section of the the document - there may be multiple of these. " +
            "Only populate the 'purposeIds' property value when the point text explicitly mentions at least one purpose - if there are no purposes mentioned in the point, 'purposeIds' value should be '[]'. As an example, 'At National Grid Reference SE 039 152 marked ‘A’ on map 1' DOES NOT contain a purpose. " +
            $"Use the following structure:\n\n[{PointOfAbstractionArrayWrapped.GetSchemaForPrompt()}]")
    };

    var systemPrompts = new List<ChatMessageContentPart>
    {
        "You are an AI assistant that extracts data from documents"
        + " and returns them as structured JSON objects. Do not return as a code block. Extract the data from this licence. Here is the licence to look at;"
        + Environment.NewLine
        + Environment.NewLine
        + allDocumentText
    };
    
    var textResponse = await GetTextResponseAsync(chatClient, modelName, systemPrompts, userPrompts);
    if (textResponse == null) throw new Exception("Some error occured");
            
    var response = JsonSerializer.Deserialize<PointOfAbstractionArrayWrapped>(textResponse, JsonHelper.GetSerializer())!;
    return response.Data;
}

async Task<BaseLicence> GetBaseLicenceDataAsync(
    ChatClient chatClient,
    string modelName,
    string allDocumentText)
{
    var userPrompts = new List<ChatMessageContentPart>
    {
        ChatMessageContentPart.CreateTextPart("If a value is not present, provide null. " +
            "The 'definitionOfYear' property and sub properties should come from a section of the document that " +
                "says something similar to 'a year means the 12 month period beginning on 1st January and ending " +
                "on 31st December' - If there is nothing like this in the document, set 'definitionOfYear' value to '[]'. " +
            /*"Property 'periodsOfAbstraction' array relates to 'period of abstraction' or similarly titled in a specific section of the the document - there may be multiple of these - DO NOT use any other section of the document for values for this property. " +
            "Property 'meansOfAbstraction' array relates to 'period of abstraction' or similarly titled in a specific section of the the document - there may be multiple of these. " +
            "Property 'periodType' value (as a sub property of 'periodsOfAbstraction') must be either 'SetPeriod' (when the text mentions when a year starts and ends, 'PerYear' (when it says 'per year' or 'all year' in the text) or null (when neither previous condition is met). " +
            "Property 'periodType' value (as a sub property of 'abstractionLimit') must be either 'PerSecond', 'PerMinute', 'PerHour', 'PerDay', 'PerWeek', 'PerMonth', 'PerYear', or 'InTotal'. " 
            "Do not populate any date fields values with minimum dates - set them as null rather then full of zeroes or empty strings. " + */
            $"Use the following structure: {BaseLicence.GetSchemaForPrompt()}")
    };

    var systemPrompts = new List<ChatMessageContentPart>
    {
        "You are an AI assistant that extracts data from documents"
        + " and returns them as structured JSON objects. Do not return as a code block. Extract the data from this licence. Here is the licence to look at;"
        + Environment.NewLine
        + Environment.NewLine
        + allDocumentText
    };
    
    var textResponse = await GetTextResponseAsync(chatClient, modelName, systemPrompts, userPrompts);
    if (textResponse == null) throw new Exception("Some error occured");
            
    return JsonSerializer.Deserialize<BaseLicence>(textResponse, JsonHelper.GetSerializer())!;
}

async Task<LicenceVersion> GetLicenceVersionAsync(
    ChatClient chatClient,
    string modelName,
    string allDocumentText)
{
    var userPrompts = new List<ChatMessageContentPart>
    {
        ChatMessageContentPart.CreateTextPart("If a value is not present, provide null. " +
            "For the 'issuer' field, use the agency or company name, rather then a personal name. " +
            "For the 'dateOfIssue' field, it may be named 'Date of Issue' in the document. " +            
            "Do not populate any date fields values with minimum dates - set them as null rather then full of zeroes. " + 
            $"Use the following structure: {LicenceVersion.GetSchemaForPrompt()}")
    };

    var systemPrompts = new List<ChatMessageContentPart>
    {
        "You are an AI assistant that extracts data from documents"
        + " and returns them as structured JSON objects. Do not return as a code block. Extract the data from this licence. Here is the licence to look at;"
        + Environment.NewLine
        + Environment.NewLine
        + allDocumentText
    };
    
    var textResponse = await GetTextResponseAsync(chatClient, modelName, systemPrompts, userPrompts);
    if (textResponse == null) throw new Exception("Some error occured");
            
    return JsonSerializer.Deserialize<LicenceVersion>(textResponse, JsonHelper.GetSerializer())!;
}

async Task<Aggregate[]> GetAggregateLimitsAsync(
    ChatClient chatClient,
    string modelName,
    string abstractionLimitsSectionText)
{
    var systemPrompts = new List<ChatMessageContentPart>
    {
        "You are an AI assistant that extracts data from documents"
        + " and returns them as structured JSON objects. Do not return as a code block. Extract the data from this licence. Here is the document to look at;"
        + Environment.NewLine
        + Environment.NewLine
        + abstractionLimitsSectionText
    };
    
    var userPrompts = new List<ChatMessageContentPart>
    {
        ChatMessageContentPart.CreateTextPart(
            "If a value is not present, provide null. " +
            "Only include limits that mention they are in 'aggregate'. " +
            "Property 'periodType' value (as a sub property of 'timePeriod') must be either 'SetPeriod' (when the text mentions when a year starts and ends, 'PerYear' (when it says 'per year' in the text) or null (when neither previous condition is met). " +
            "Property 'periodType' value (as a sub property of 'limits') must be either 'PerSecond', 'PerMinute', 'PerHour', 'PerDay', 'PerWeek', 'PerMonth', 'PerYear', or 'InTotal'. " +
            "Property 'primaryType' value must be either 'InLicence' (if there is no other licence mentioned) or 'LicenceToLicence' (when there is another licence mentioned). " +
            "Property 'subType' value must be either 'PurposeToPurpose' (when a purpose is mentioned in the limit), 'PointToPoint' (when a point is mentioned in the limit) or null  (when neither a point or purpose is mentioned in the limit). " +
            "Property 'cutoffType' must be either 'Upto' (if the text says 'upto'), 'From' (if the text says 'from') or null. " +
            "Only populate the 'points' property value when the text explicitly mentions at least one point - if there are no point mentioned in the limit, 'points' value should be '[]'. " +
            "Only populate the 'purposes' property value when the text explicitly mentions at least one purpose - if there are no purpose mentioned in the limit, 'purposes' value should be '[]'. " +
            "You should return '[]' if the document does not include the word 'aggregate'. " +
            $"Use the following structure:\n\n[{AggregateArrayWrapped.GetSchemaForPrompt()}]"
        )
    };
            
    var textResponse = await GetTextResponseAsync(chatClient, modelName, systemPrompts, userPrompts);
    if (textResponse == null) throw new Exception("Some error occured");

    var aggregateLimits = JsonSerializer.Deserialize<AggregateArrayWrapped>(textResponse, JsonHelper.GetSerializer())!;
    return aggregateLimits.Data;
}

async Task<PeriodOfAbstraction[]> GetPeriodsOfAbstractionAsync(
    ChatClient chatClient,
    string modelName,
    string abstractionLimitsSectionText)
{
    var systemPrompts = new List<ChatMessageContentPart>
    {
        "You are an AI assistant that extracts data from documents"
        + " and returns them as structured JSON objects. Do not return as a code block. Extract the data from this licence. Here is the document to look at;"
        + Environment.NewLine
        + Environment.NewLine
        + abstractionLimitsSectionText
    };
    
    var userPrompts = new List<ChatMessageContentPart>
    {
        ChatMessageContentPart.CreateTextPart(
            "If a value is not present, provide null. " +
            "This array relates to 'periods of abstraction' or similarly titled in a specific section of the the document - there may be multiple of these - DO NOT use any other section of the document for values for this property " +
            $"Use the following structure:\n\n[{PeriodOfAbstractionArrayWrapped.GetSchemaForPrompt()}]"
        )
    };
            
    var textResponse = await GetTextResponseAsync(chatClient, modelName, systemPrompts, userPrompts);
    if (textResponse == null) throw new Exception("Some error occured");

    var periodsOfAbstraction = JsonSerializer.Deserialize<PeriodOfAbstractionArrayWrapped>(textResponse, JsonHelper.GetSerializer())!;
    return periodsOfAbstraction.Data;
}

async Task<MeanOfAbstraction[]> GetMeansOfAbstractionAsync(
    ChatClient chatClient,
    string modelName,
    string abstractionLimitsSectionText)
{
    var systemPrompts = new List<ChatMessageContentPart>
    {
        "You are an AI assistant that extracts data from documents"
        + " and returns them as structured JSON objects. Do not return as a code block. Extract the data from this licence. Here is the document to look at;"
        + Environment.NewLine
        + Environment.NewLine
        + abstractionLimitsSectionText
    };
    
    var userPrompts = new List<ChatMessageContentPart>
    {
        ChatMessageContentPart.CreateTextPart(
            "If a value is not present, provide null. " +
            "This array relates to the 'means of abstraction' or similarly titled in a specific section of the the document - there may be multiple array items under this. " +
            "Property 'abstractionLimit' value should be 'null' UNLESS there are limits mentioned that relate to how quickly water can be abstracted. " +
            "Provide one array item for each mean of abstraction mentioned. " +
            "Property 'periodType' value must be either 'PerSecond', 'PerMinute', 'PerHour', 'PerDay', 'PerWeek', 'PerMonth', 'PerYear', or 'InTotal'. " +
            $"Use the following structure:\n\n[{MeanOfAbstractionArrayWrapped.GetSchemaForPrompt()}]"
        )
    };
            
    var textResponse = await GetTextResponseAsync(chatClient, modelName, systemPrompts, userPrompts);
    if (textResponse == null) throw new Exception("Some error occured");

    var meansOfAbstraction = JsonSerializer.Deserialize<MeanOfAbstractionArrayWrapped>(textResponse, JsonHelper.GetSerializer())!;
    return meansOfAbstraction.Data;
}

async Task<AbstractionLimit[]> GetIndividualAbstractionLimitsAsync(
    ChatClient chatClient,
    string modelName,
    string abstractionLimitsSectionText)
{
    var systemPrompts = new List<ChatMessageContentPart>
    {
        "You are an AI assistant that extracts data from documents"
        + " and returns them as structured JSON objects. Do not return as a code block. Extract the data from this licence. Here is the document to look at;"
        + Environment.NewLine
        + Environment.NewLine
        + abstractionLimitsSectionText
    };
    
    var userPrompts = new List<ChatMessageContentPart>
    {
        ChatMessageContentPart.CreateTextPart(
            "If a value is not present, provide null. " +
            "Only populate the 'points' property value when the text explicitly mentions at least one point - if there are no point mentioned in the limit, 'points' value should be '[]'. " +
            "Only populate the 'purposes' property value when the text explicitly mentions at least one purpose - if there are no purpose mentioned in the limit, 'purposes' value should be '[]'. " +
            "Exclude any limits that mention they are in aggregate. " +
            "Property 'periodType' value must be either 'PerSecond', 'PerMinute', 'PerHour', 'PerDay', 'PerWeek', 'PerMonth', 'PerYear', or 'InTotal'. " +
            $"Use the following structure:\n\n[{AbstractionLimitArrayWrapped.GetSchemaForPrompt()}]"
        )
    };
            
    var textResponse = await GetTextResponseAsync(chatClient, modelName, systemPrompts, userPrompts);
    if (textResponse == null) throw new Exception("Some error occured");

    var individualAbstractionLimits = JsonSerializer.Deserialize<AbstractionLimitArrayWrapped>(textResponse, JsonHelper.GetSerializer())!;
    return individualAbstractionLimits.Data;
}

async Task<string?> GetAbstractionLimitsTextAsync(
    ChatClient chatClient,
    string modelName,
    string allDocumentText)
{
    var userPrompts = new List<ChatMessageContentPart>
    {
        ChatMessageContentPart.CreateTextPart(
            "Please fetch me the whole section of the document that covers how much water is allowed to be pulled per day, per year etc... It may mention cubic metres etc... Do "
            + "not change it at all. Give me only this - do not add any follow up questions or advice. Here is the document to look at;"
            + Environment.NewLine
            + Environment.NewLine
            + allDocumentText)
    };
    
    return await GetTextResponseAsync(
        chatClient,
        modelName,
        [
            "You are an AI assistant that extracts a section of text from documents"
            + " and returns them as is. Return only this text, with no other instructions or text." ],
        userPrompts,
        false);
}

async Task<string?> GetDocumentTextAsync(
    ChatClient chatClient,
    string modelName,
    List<ChatMessageContentPart> imagePrompts)
{
    var userPrompts = new List<ChatMessageContentPart>
    {
        ChatMessageContentPart.CreateTextPart(
            "Please fetch me this whole document as text. Do "
            + "not change it at all. Give me only this - do not add any follow up questions or advice. Do not add markdown."
        )
    };
    
    userPrompts.AddRange(imagePrompts);
            
    var response = await GetTextResponseAsync(
        chatClient,
        modelName,
        [
            "You are an AI assistant that extracts a the text from documents"
            + " and returns it as is. Return only this text, with no other instructions or text." ],
        userPrompts,
        false);

    return response?.Trim();
}

async Task<List<ChatMessageContentPart>> GetImagePromptsAsync(
    string pdfFilename,
    List<List<SKBitmap>> pageImageGroups)
{
    var tesseractOcr = new TesseractOcrDataExtractorService(
        KeyConfig.TesseractPrefix
        ?? throw new NullReferenceException(KeyConfig.TesseractPrefix));

    var mockPdfDocument = new PdfDocument(
        "[NOT_USED]",
        KeyConfig.OutputFolder,
        KeyConfig.CacheFolder,
        true);
    
    var imagePrompts = new List<ChatMessageContentPart>();
            
    Directory.CreateDirectory("Cache/PDFtoImage/Images");
    var pageNumber = 0;

    foreach (var pageImageGroup in pageImageGroups)
    {
        var filename = $"{pdfFilename.Replace(".", "_")}_{pageNumber}.jpg";
        var pdfImageName = $"Cache/PDFtoImage/Images/{filename}";

        var regenerateImage = true;

        if (regenerateImage)
        {
            var totalHeight = pageImageGroup.Sum(image => image.Height);
            var width = pageImageGroup.Max(image => image.Width);
            var stitchedImage = new SKBitmap(width, totalHeight);
            var canvas = new SKCanvas(stitchedImage);
            var currentHeight = 0;

            foreach (var pageImage in pageImageGroup)
            {
                canvas.DrawBitmap(pageImage, 0, currentHeight);
                currentHeight += pageImage.Height;
            }

            await using var stitchedFileStream = new FileStream(pdfImageName, FileMode.Create, FileAccess.Write);
            stitchedImage.Encode(stitchedFileStream, SKEncodedImageFormat.Jpeg, 100);
        }

        var lines =
            (await tesseractOcr.GetTextLinesFromImageAsync(
                pdfImageName,
                pageNumber,
                1,
                mockPdfDocument)).ToList();

        var averageLineLength = lines.Average(line
            => line.Text.Length);

        // Short lines indicate it may be a map page, no point processing that
        if (averageLineLength < 30)
        {
            pageNumber += 1;
            continue;
        }

        var imageBytes = await File.ReadAllBytesAsync(pdfImageName);
        
        imagePrompts.Add(ChatMessageContentPart.CreateImagePart(
            BinaryData.FromBytes(imageBytes),
            "image/jpeg",
            ChatImageDetailLevel.Auto));

        pageNumber += 1;
    }

    tesseractOcr.Dispose();
    return imagePrompts;
}

async Task<string?> GetTextResponseAsync(
    ChatClient chatClient,
    string modelName,
    List<ChatMessageContentPart> systemPrompts,
    List<ChatMessageContentPart> userPrompts,
    bool json = true)
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
            ResponseFormat = json ? ChatResponseFormat.CreateJsonObjectFormat() : ChatResponseFormat.CreateTextFormat()
        });

    var textResponse = chatResponse.Value?.Content.FirstOrDefault()?.Text;

    if (json)
    {
        if (chatResponse.Value?.FinishReason != ChatFinishReason.Stop)
        {
            Console.WriteLine($"ERROR - Response truncated {chatResponse.Value?.FinishReason}");
            Console.Write(textResponse);

            return null;
        }

        if (!textResponse!.EndsWith('}'))
        {
            Console.WriteLine($"ERROR - Malformed JSON returned {chatResponse.Value?.FinishReason}");
            Console.Write(textResponse);

            return null;
        }
    }

    return textResponse;
}

int GetMaxTokens(
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

async Task GenerateCsvForTestingAsync()
{
    var pdfDataExtractor = new PdfDataExtractorService(
        new PdfPigNoOcrDataExtractorService(),
        new List<IOcrDataExtractorService>
        {
            new AzureAiVisionOcrDataExtractorService(
                KeyConfig.AiVisionEndpoint,
                KeyConfig.AiVisionKey)
        },
        KeyConfig.PdfFolder);

    //var data = await GetYorkshire70DataAsync(pdfDataExtractor);
    var data = await GetYorkshire6DataAsync(pdfDataExtractor);

    await using var writer = new StreamWriter($"Yorkshire-{DateTime.Today.ToString("yyyyMMdd")}.csv");
    await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

    csv.WriteRecords((IEnumerable)data);
}

Task<MatchesResult> GetMatchesAsync(string fileName, PdfDataExtractorService pdfDataExtractor)
{
    Dictionary<string, string> fileLicenceMapping = new() {{"", ""}};
    var pdfFolder = KeyConfig.PdfFolder;
    
    return pdfDataExtractor.GetMatchesAsync(
        pdfFolder + fileName,
        new LookupConfiguration(
            LabelConfiguration.GetLabels(),
            fileLicenceMapping,
            "Output/",
            "Cache/"),
        [pdfFolder + fileName]);
}

async Task<List<CsvLine>> GetYorkshire70DataAsync(PdfDataExtractorService pdfDataExtractor)
{
    var yorkshire = YorkshireFiles();
    
    var pdfFilePaths = Directory
        .GetFiles(KeyConfig.PdfFolder)
        .Where(fileName => fileName.EndsWith(".pdf", StringComparison.InvariantCultureIgnoreCase))
        .Where(x =>
        {
            var filename = x.Split('/').Last();
            return yorkshire.Contains(filename, StringComparer.InvariantCultureIgnoreCase);
            
        })
        .Select(x => x.Split('/').Last())
        .OrderBy(x => x).ToList();

    var returnList = new List<CsvLine>();
    
    foreach (var pdfFilePath in pdfFilePaths)
    {
        var internalJson = await GetMatchesAsync(pdfFilePath, pdfDataExtractor);
        var file = SchemaConverter.ToLicenceGroup(internalJson).Licences[0];
        
        returnList.Add(new()
        {
            Filename = file.Filename,
            LicenceNumber = file.LicenceNumber,
            HasAggregate = file.AbstractionLimits.Aggregates.Length > 0,
            AggregateData = JsonSerializer.Serialize(file.AbstractionLimits.Aggregates),
            Data = JsonSerializer.Serialize(file)
        });
    }

    return returnList;
}

async Task<List<CsvLine>> GetYorkshire6DataAsync(PdfDataExtractorService pdfDataExtractor)
{
    var internalJson = await GetMatchesAsync("2-26-32-126 6937559.PDF", pdfDataExtractor);
    var file1 = SchemaConverter.ToLicenceGroup(internalJson).Licences[0];

    internalJson = await GetMatchesAsync("2-27-29-012 7003124.PDF", pdfDataExtractor);
    var file2 = SchemaConverter.ToLicenceGroup(internalJson).Licences[0];

    internalJson = await GetMatchesAsync("Application - New - Licence Issued 30092021.pdf", pdfDataExtractor);
    var file3 = SchemaConverter.ToLicenceGroup(internalJson).Licences[0];

    internalJson = await GetMatchesAsync("Application Formal Variation Issued Licence 07032023 (1).pdf", pdfDataExtractor);
    var file4 = SchemaConverter.ToLicenceGroup(internalJson).Licences[0];

    internalJson = await GetMatchesAsync("Application Formal Variation Issued Licence 07032023.pdf", pdfDataExtractor);
    var file5 = SchemaConverter.ToLicenceGroup(internalJson).Licences[0];

    internalJson = await GetMatchesAsync("Application Minor Variation Issued Licence 03.10.24.pdf", pdfDataExtractor);
    var file6 = SchemaConverter.ToLicenceGroup(internalJson).Licences[0];

    return
    [
        new()
        {
            Filename = file1.Filename,
            LicenceNumber = file1.LicenceNumber,
            HasAggregate = file1.AbstractionLimits.Aggregates.Length > 0,
            AggregateData = JsonSerializer.Serialize(file1.AbstractionLimits.Aggregates),
            Data = JsonSerializer.Serialize(file1)
        },
        new()
        {
            Filename = file2.Filename,
            LicenceNumber = file2.LicenceNumber,
            HasAggregate = file2.AbstractionLimits.Aggregates.Length > 0,
            AggregateData = JsonSerializer.Serialize(file2.AbstractionLimits.Aggregates),
            Data = JsonSerializer.Serialize(file2)
        },
        new()
        {
            Filename = file3.Filename,
            LicenceNumber = file3.LicenceNumber,
            HasAggregate = file3.AbstractionLimits.Aggregates.Length > 0,
            AggregateData = JsonSerializer.Serialize(file3.AbstractionLimits.Aggregates),
            Data = JsonSerializer.Serialize(file3)
        },
        new()
        {
            Filename = file4.Filename,
            LicenceNumber = file4.LicenceNumber,
            HasAggregate = file4.AbstractionLimits.Aggregates.Length > 0,
            AggregateData = JsonSerializer.Serialize(file4.AbstractionLimits.Aggregates),
            Data = JsonSerializer.Serialize(file4)
        },
        new()
        {
            Filename = file5.Filename,
            LicenceNumber = file5.LicenceNumber,
            HasAggregate = file5.AbstractionLimits.Aggregates.Length > 0,
            AggregateData = JsonSerializer.Serialize(file5.AbstractionLimits.Aggregates),
            Data = JsonSerializer.Serialize(file5)
        },
        new()
        {
            Filename = file6.Filename,
            LicenceNumber = file6.LicenceNumber,
            HasAggregate = file6.AbstractionLimits.Aggregates.Length > 0,
            AggregateData = JsonSerializer.Serialize(file6.AbstractionLimits.Aggregates),
            Data = JsonSerializer.Serialize(file6)
        }
    ];
}

List<string> YorkshireFiles()
{
    return
    [
        "22713185__Non-Application Licence Documents (20.12.1996).pdf",
        "22714090r01__Application Transfer Issued Licence 12 6 24 12 6 24.pdf",
        "22718033__Application - Minor Variation - Issued Licence - 16022023.pdf",
        "22718045__Application - Reduction -Application New Licence Issued 24_06_2019 00_00_00 10897641.pdf",
        "22718125R01__Application - NA Formal Variation - Issued Licence 31.03.21 11764153.pdf",
        "22718131r01__Application -New   licence - Issued Licence  - PDR- 15.12.2022.pdf",
        "22724197__Application - NA Formal Variation - Issued Licence 02112022.pdf",
        "NE0270012011__Application - New - Issued Licence 02.12.2013 8110044.pdf",
        "NE0270012049__Application – New Full   – Issued Licence 23122022.pdf",
        "ne0270018009__Application – Formal Variation – Issued Licence 19122022.pdf",
        "ne0270018020__Application - Minor Variation - Issued Licence - 16022023.pdf",
        "ne0270018023__Application - Minor Variation -Issued Licence - 08.11.2022.pdf",
        "ne0270018033__Application – Formal Variation – Issued Licence 1512022.pdf",
        "NE0270018041__Application NA New Issued Licence 26 03 2021 11761845.pdf",
        "22725124__Non-Application Licence Document (09.10.2008).pdf",
        "22727116__Application Formal Variation Issued Licence - 26092023.pdf",
        "22727278__Non-Application Licence Document (26.01.2009).pdf",
        "22727279__Non-Application Licence Document (26.01.2009).pdf",
        "ne0270025032__Application New Issued Licence 16.05.23.pdf",
        "NE0270025037__Application Formal Variation Issued Licence 16.05.23.pdf",
        "NE0270026005R01__Application Renewal Licence Issued - (25092024).pdf",
        "ne0270027009__Application Formal Variation Issued Licence 03.05.23.pdf",
        "ne0270028073__Application – NA New – Issued Licence 27092022.pdf",
        "NE0270028081__Application New License - License Issued - 18102024.pdf",
        "22704027r01__Application Formal Variation Issued Licence - [issued date] - (07062024).pdf",
        "22707004__Application - Transfer - Issued Licence 28.04.2017 9774748.pdf",
        "22708092__Application – NA Formal Variation – Issued Licence-10082022.pdf",
        "22709099__Application Minor Variation Licence issued 21.12.2018 10629856.pdf",
        "22709196r01__Application New Licence Issued - [22.03.2024] - (22.03.2024).pdf",
        "NE0270005031__Application New Issued Licence 17.04.23.pdf",
        "NE0270029007R01__Application Renewal Licence Issued - [issued date] - (11042024).pdf",
        "22631093__Application - Issued Licence [23-10-1978] 6075944.pdf",
        "22631097__Non-Application Licence Document (09.03.1988).pdf",
        "22631114__Application Formal Variation Issued Licence - [issued date] - (29082024).pdf",
        "22631168R01__Application Renewal Licence Issued - [issued date] - (09052024).pdf",
        "22632004__Application Minor Variation Issued Licence - 06122023.pdf",
        "22632235__Application Renewal - Licence Issued - 11112024.pdf",
        "22632344__Application - NA Formal Variation - Issued Licence 27102022.pdf",
        "22634031__Application - NA Formal Variation - Issued Licence 27102022.pdf",
        "22724007__Application minor variation issued Licence 22724007 11600563.pdf",
        "NE0260030016R01__Application Renewal - Licence Issued - 20112024.pdf",
        "NE0260031035__Application New Issued Licence 28.04.2023.pdf",
        "ne0260032055__Application - NA New - Issued Licence 15112022.pdf",
        "NE0260032058__Application NA New Licence Issued (Public Register) - 02122022 .pdf",
        "NE0260032074__Application  new  -licence issued  (08072024).pdf",
        "NE0260033011__Application - New -Application New Licence Issued 24_03_2020 00_00_00 11292824.pdf",
        "NE0260033017__Application Formal Variation - Licence Issued - (23052024).pdf",
        "NE0260034006__Application - Formal Variation -Application New Licence Issued 08_08_2019 00_00_00 10974057.pdf",
        "NE0260034018__Application Minor Variation Issued Licence 11.12.2019 11149535.pdf",
        "NE0260034052__Application Apportionment Issued Licence 11.12.2019 11149440.pdf",
        "NE0260034056__Application New Issued Licence 10.09.2020 11497061.pdf",
        "NE0270024021R02__Application Renewal Licence Issued - 20062024.pdf",
        "22721238__Non-Application Licence Document (25.07.1977).pdf",
        "22721348r01__Application – NA Formal Variation – Issued Licence 13.07.2022.pdf",
        "22721356R01__Application Formal Variation Issued Licence 13.9.18 10487468.pdf",
        "22722128__Non-Application Licence Document (15.08.1988).pdf",
        "22722323__Non-Application Licence Document - Issued Licence - 22101998.pdf",
        "22722395A__Non-Application Licence Document (22.10.2001).pdf",
        "22722452__Non-Application Licence Document [Issued Licence] (26.2.01).pdf",
        "22722460__Application New Licence Issued [17.1.1992] (26.7.2010).pdf",
        "22722580r01__Application Transfer - Issued Licence 24092021.pdf",
        "22723556__Application - Formal Variation -Application New Licence Issued 12_04_2019 00_00_00 10797059.pdf",
        "ne0270021016__Application - Minor Variation -Application New Licence Issued 12_03_2021 00_00_00 11736007.pdf",
        "NE0270022058__Application New Issued Licence 18.05.23.pdf",
        "NE0270023043__Application New Licence Issued 18.12.2018 10623801.pdf",
        "NE0270023047__Application - New -Application New Licence Issued 06_04_2020 00_00_00 11303354.pdf",
        "22719149__Application Formal Variation - Issued Licence [04-09-2018] 10474343.pdf",
        "22719156__Application Formal Variation Licence Issued - 12102023.pdf",
        "22720093__Non-Application Licence Document (02.02.1998).pdf",
        "22720211__Non-Application Licence Document (01.12.1990).pdf",
        "22724371r01__Application NA Formal Variation Issued Licence 21122021.pdf",
        "NE0270020038__Application - New Licence Issued - Licence Issued - PDF - 28.10.2022.pdf",
        "NE0270020044__Application New Licence Issued - 20112024.pdf"
    ];
}

internal class CsvLine
{
    public string? Filename { get; set; }
    public string? LicenceNumber { get; set; }
    public bool HasAggregate { get; set; }
    public string? AggregateData { get; set; }
    public string? Data { get; set; }
}

internal class PointOfAbstractionArrayWrapped
{
    public PointOfAbstraction[] Data { get; init; } = [];
    
    public static string GetSchemaForPrompt()
    {
        var template = new PointOfAbstractionArrayWrapped { Data = [PointOfAbstraction.Template] };
        return JsonSerializer.Serialize(template, JsonHelper.GetSerializer());
    }
}

internal class PurposeOfAbstractionArrayWrapped
{
    public PurposeOfAbstraction[] Data { get; init; } = [];
    
    public static string GetSchemaForPrompt()
    {
        var template = new PurposeOfAbstractionArrayWrapped { Data = [PurposeOfAbstraction.Template] };
        return JsonSerializer.Serialize(template, JsonHelper.GetSerializer());
    }
}

internal class AbstractionLimitArrayWrapped
{
    public AbstractionLimit[] Data { get; init; } = [];
    
    public static string GetSchemaForPrompt()
    {
        var template = new AbstractionLimitArrayWrapped { Data = [AbstractionLimit.Template] };
        return JsonSerializer.Serialize(template, JsonHelper.GetSerializer());
    }
}

internal class AggregateArrayWrapped
{
    public Aggregate[] Data { get; init; } = [];
    
    public static string GetSchemaForPrompt()
    {
        var template = new AggregateArrayWrapped { Data = [Aggregate.Template] };
        return JsonSerializer.Serialize(template, JsonHelper.GetSerializer());
    }
}

internal class PeriodOfAbstractionArrayWrapped
{
    public PeriodOfAbstraction[] Data { get; init; } = [];
    
    public static string GetSchemaForPrompt()
    {
        var template = new PeriodOfAbstractionArrayWrapped { Data = [PeriodOfAbstraction.Template] };
        return JsonSerializer.Serialize(template, JsonHelper.GetSerializer());
    }
}

internal class MeanOfAbstractionArrayWrapped
{
    public MeanOfAbstraction[] Data { get; init; } = [];
    
    public static string GetSchemaForPrompt()
    {
        var template = new MeanOfAbstractionArrayWrapped { Data = [MeanOfAbstraction.Template] };
        return JsonSerializer.Serialize(template, JsonHelper.GetSerializer());
    }
}

// Not currently supported: - Images can't be uploaded as a file and then referenced as input. Coming soon.
// https://learn.microsoft.com/en-us/azure/ai-foundry/openai/how-to/responses?tabs=rest-api- No point in doing this yet
/*var uploadResult = await fileClient.UploadFileAsync(
    BinaryData.FromBytes(imageBytes),
    filename,
    FileUploadPurpose.Vision
);*/

// Not currently supported: - Images can't be uploaded as a file and then referenced as input. Coming soon.
// https://learn.microsoft.com/en-us/azure/ai-foundry/openai/how-to/responses?tabs=rest-api- No point in doing this yet
//var fileClient = azureClient.GetOpenAIFileClient();