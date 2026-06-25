using System.ClientModel;
using System.Text.Json;
using Azure.AI.OpenAI;
using Microsoft.ML.Tokenizers;
using OpenAI.Chat;
using PDFtoImage;
using SkiaSharp;
using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OutputSchema;
using WALE.ProcessFile.Services.Cache;
using WALE.ProcessFile.Services.Docnet;
using WALE.ProcessFile.Services.Models.OutputSchema.PromptSpecific;
using WALE.ProcessFile.Services.Output;
using WALE.ProcessFile.Services.PdfPig;
using WALE.ProcessFile.Services.Services;
using WALE.ProcessFile.Services.Tesseract;
using WALE.Tools.Config;

namespace WALE.Tools._2ndHalf;

public static class TestsForAiPrompts
{
    public static async Task TestsForAiPromptsAsync()
    {
        var modelName = "gpt-4o"; // gpt-4o-mini gets stuck it seems
        var deploymentName = "gpt-4o"; // gpt-4o-mini gets stuck it seems
        
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

                var chatClient = azureClient.GetChatClient(deploymentName);
                var cacheService = new FileSystemCacheService("Cache/");
                
                var imagePrompts = await GetImagePromptsAsync(
                    pdfFilename,
                    pageImageGroups,
                    new LookupConfiguration(
                        [],
                        [],
                        [],
                        [],
                        new LocalFileService(KeyConfig.PdfFolder),
                        cacheService,
                        -1));
                
                ConsoleHelper.WriteLine($"Getting all document text from {imagePrompts.Count} pages");
                
                var allDocumentText = await GetDocumentTextAsync(
                    chatClient,
                    modelName,
                    imagePrompts);

                ConsoleHelper.WriteLine($"Found document text - {allDocumentText?.Length} lines");
                
                if (string.IsNullOrEmpty(allDocumentText))
                {
                    ConsoleHelper.WriteLine("Moving on to the next record");
                    break;
                }
                
                ConsoleHelper.WriteLine("Looking up abstraction limits section");
                
                var abstractionLimitsSectionText = await GetAbstractionLimitsTextAsync(
                    chatClient,
                    modelName,
                    allDocumentText);

                ConsoleHelper.WriteLine($"Found abstraction limits section - {abstractionLimitsSectionText?.Length} lines");
                
                if (string.IsNullOrEmpty(abstractionLimitsSectionText))
                {
                    ConsoleHelper.WriteLine("Moving on to the next record");
                    break;
                }
                
                ConsoleHelper.WriteLine("Looking up points");
                var pointsTask = GetPointsAsync(chatClient, modelName, allDocumentText);
                
                ConsoleHelper.WriteLine("Looking up purposes");
                var purposesTask = GetPurposesAsync(chatClient, modelName, allDocumentText);
                
                ConsoleHelper.WriteLine("Looking up licence version");
                var licenceVersionTask = GetLicenceVersionAsync(chatClient, modelName, allDocumentText);
                
                ConsoleHelper.WriteLine("Looking up general licence data");
                var baseLicenceDataTask = GetBaseLicenceDataAsync(chatClient, modelName, allDocumentText);
                
                ConsoleHelper.WriteLine("Looking up means of abstraction");
                var meansOfAbstractionTask = GetMeansOfAbstractionAsync(chatClient, modelName, allDocumentText);
                
                ConsoleHelper.WriteLine("Looking up periods of abstraction");
                var periodsOfAbstractionTask = GetPeriodsOfAbstractionAsync(chatClient, modelName, allDocumentText);

                var points = await pointsTask;
                ConsoleHelper.WriteLine($"Found {points.Length} points");
                
                var purposes = await purposesTask;
                ConsoleHelper.WriteLine($"Found {purposes.Length} purposes");
                
                ConsoleHelper.WriteLine("Looking up individual limits");
                var individualLimitsTask = GetIndividualAbstractionLimitsAsync(
                    chatClient,
                    modelName,
                    abstractionLimitsSectionText,
                    points,
                    purposes);
                
                ConsoleHelper.WriteLine("Looking up aggregate limits");
                var aggregateLimitsTask = GetAggregateLimitsAsync(
                    chatClient,
                    modelName,
                    abstractionLimitsSectionText,
                    points,
                    purposes);
                
                var individualLimits = await individualLimitsTask;
                ConsoleHelper.WriteLine($"Found {individualLimits.Length} individual abstraction limit(s)");
                
                var aggregateLimits = await aggregateLimitsTask;
                ConsoleHelper.WriteLine($"Found {aggregateLimits.Length} aggregate limit(s)");
                
                var meansOfAbstraction = await meansOfAbstractionTask;
                ConsoleHelper.WriteLine($"Found {meansOfAbstraction.Length} means of abstraction");
                
                var periodsOfAbstraction = await periodsOfAbstractionTask;
                ConsoleHelper.WriteLine($"Found {periodsOfAbstraction.Length} periods of abstraction");
                
                var licenceVersion =  await licenceVersionTask;
                ConsoleHelper.WriteLine("Found licence version");
                
                var baseLicenceData = await baseLicenceDataTask;
                ConsoleHelper.WriteLine("Found general licence data");
                
                var schema = new Licence
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
                    LicenceNumber = new ValueWithConfidence<string>(
                        baseLicenceData.LicenceNumber, -1, -1),
                    MeansOfAbstraction = meansOfAbstraction,
                    DefinitionOfYear = baseLicenceData.DefinitionOfYear,
                    PeriodsOfAbstraction = periodsOfAbstraction,
                    RegionId = -1 // TODO
                };
                
                var filenameNoExtension = pdfFilename.Split('.').First();
                var filenameNoSpacesOrDashes = filenameNoExtension
                    .Replace("-", string.Empty)
                    .Replace(" ", string.Empty);

                var json = JsonSerializer.Serialize(schema, JsonHelper.GetSerializerOptions());
                var outputJs = $"window.aiData['{filenameNoSpacesOrDashes}'] = {json};";
                
                await File.WriteAllTextAsync(filenameNoExtension + ".js", outputJs);
                Console.Write(outputJs);
            }
            catch (Exception e)
            {
                ConsoleHelper.WriteLine(e);
                throw;
            }
        }
    }

    static async Task<PurposeOfAbstraction[]> GetPurposesAsync(
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
                
        var response = JsonSerializer.Deserialize<PurposeOfAbstractionArrayWrapped>(textResponse, JsonHelper.GetSerializerOptions())!;
        return response.Data;
    }

    static async Task<PointOfAbstraction[]> GetPointsAsync(
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
                
        var response = JsonSerializer.Deserialize<PointOfAbstractionArrayWrapped>(textResponse, JsonHelper.GetSerializerOptions())!;
        return response.Data;
    }

    static async Task<BaseLicence> GetBaseLicenceDataAsync(
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
                
        return JsonSerializer.Deserialize<BaseLicence>(textResponse, JsonHelper.GetSerializerOptions())!;
    }

    static async Task<LicenceVersion> GetLicenceVersionAsync(
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
                
        return JsonSerializer.Deserialize<LicenceVersion>(textResponse, JsonHelper.GetSerializerOptions())!;
    }

    static async Task<Aggregate[]> GetAggregateLimitsAsync(
        ChatClient chatClient,
        string modelName,
        string abstractionLimitsSectionText,
        PointOfAbstraction[] points,
        PurposeOfAbstraction[] purpose)
    {
        var systemPrompts = new List<ChatMessageContentPart>
        {
            "You are an AI assistant that extracts data from documents"
            + " and returns them as structured JSON objects. Do not return as a code block. Extract the data from this licence. Here is the document to look at;"
            + Environment.NewLine
            + Environment.NewLine
            + abstractionLimitsSectionText
            + Environment.NewLine
            + Environment.NewLine
            + "Here is the points of abstraction information in JSON format to use to enrich the relevant parts;"
            + JsonSerializer.Serialize(points, JsonHelper.GetSerializerOptions())
            + Environment.NewLine
            + Environment.NewLine
            + "Here is the purpose of abstraction information in JSON format to use to enrich the relevant parts;"
            + JsonSerializer.Serialize(purpose, JsonHelper.GetSerializerOptions())    
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

        var aggregateLimits = JsonSerializer.Deserialize<AggregateArrayWrapped>(textResponse, JsonHelper.GetSerializerOptions())!;
        return aggregateLimits.Data;
    }

    static async Task<PeriodOfAbstraction[]> GetPeriodsOfAbstractionAsync(
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
                "If a value is not present, provide null. If an array item is null, exclude it. " +
                "This array relates to 'periods of abstraction' or similarly titled in a specific section of the the document - there may be multiple of these - DO NOT use any other section of the document for values for this property " +
                $"Use the following structure:\n\n[{PeriodOfAbstractionArrayWrapped.GetSchemaForPrompt()}]"
            )
        };
                
        var textResponse = await GetTextResponseAsync(chatClient, modelName, systemPrompts, userPrompts);
        if (textResponse == null) throw new Exception("Some error occured");

        var periodsOfAbstraction = JsonSerializer.Deserialize<PeriodOfAbstractionArrayWrapped>(textResponse, JsonHelper.GetSerializerOptions())!;
        return periodsOfAbstraction.Data;
    }

    static async Task<MeanOfAbstraction[]> GetMeansOfAbstractionAsync(
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

        var meansOfAbstraction = JsonSerializer.Deserialize<MeanOfAbstractionArrayWrapped>(textResponse, JsonHelper.GetSerializerOptions())!;
        return meansOfAbstraction.Data;
    }

    static async Task<AbstractionLimitGroup[]> GetIndividualAbstractionLimitsAsync(
        ChatClient chatClient,
        string modelName,
        string abstractionLimitsSectionText,
        PointOfAbstraction[] points,
        PurposeOfAbstraction[] purpose)
    {
        var systemPrompts = new List<ChatMessageContentPart>
        {
            "You are an AI assistant that extracts data from documents"
            + " and returns them as structured JSON objects. Do not return as a code block. Extract the data from this licence. Here is the document to look at;"
            + Environment.NewLine
            + Environment.NewLine
            + abstractionLimitsSectionText
            + Environment.NewLine
            + Environment.NewLine
            + "Here is the points of abstraction information in JSON format to use to enrich the relevant parts;"
            + JsonSerializer.Serialize(points, JsonHelper.GetSerializerOptions())
            + Environment.NewLine
            + Environment.NewLine
            + "Here is the purpose of abstraction information in JSON format to use to enrich the relevant parts;"
            + JsonSerializer.Serialize(purpose, JsonHelper.GetSerializerOptions())            
        };
        
        var userPrompts = new List<ChatMessageContentPart>
        {
            ChatMessageContentPart.CreateTextPart(
                "If a value is not present, provide null. " +
                "Only populate the 'points' property value when the text explicitly mentions at least one point - if there are no point mentioned in the limit, 'points' value should be '[]'. " +
                "Only populate the 'purposes' property value when the text explicitly mentions at least one purpose - if there are no purpose mentioned in the limit, 'purposes' value should be '[]'. " +
                "Exclude any limits that mention they are in aggregate. " +
                "Property 'periodType' value must be either 'PerSecond', 'PerMinute', 'PerHour', 'PerDay', 'PerWeek', 'PerMonth', 'PerYear', or 'InTotal'. " +
                $"Use the following structure:\n\n[{AbstractionLimitGroupArrayWrapped.GetSchemaForPrompt()}]"
            )
        };
                
        var textResponse = await GetTextResponseAsync(chatClient, modelName, systemPrompts, userPrompts);
        if (textResponse == null) throw new Exception("Some error occured");

        var individualAbstractionLimits = JsonSerializer.Deserialize<AbstractionLimitGroupArrayWrapped>(textResponse, JsonHelper.GetSerializerOptions())!;
        return individualAbstractionLimits.Data;
    }

    static async Task<string?> GetAbstractionLimitsTextAsync(
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

    static async Task<string?> GetDocumentTextAsync(
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

    static async Task<List<ChatMessageContentPart>> GetImagePromptsAsync(
        string pdfFilename,
        List<List<SKBitmap>> pageImageGroups,
        LookupConfiguration lookupConfiguration)
    {
        var cacheService = new FileSystemCacheService("Cache/");
        var outputService = new FileSystemOutputService("Output/");
        
        var pdfPigDocumentService = new PdfPigNoOcrPdfDocumentService();
        var docnetAlternativeDocumentService = new DocnetNoOcrAlternativePdfDocumentService();
        
        var tesseractOcr = new TesseractOcrDataExtractorService(
            KeyConfig.TesseractPrefix
                ?? throw new NullReferenceException(KeyConfig.TesseractPrefix),
                ProcessFile.Core.Enums.PageSegMode.SparseTextOsd,
            cacheService,
            outputService,
            KeyConfig.DotnetPath,
            KeyConfig.TesseractExeName,
            KeyConfig.TesseractExeDirectory);

        var mockPdfDocument = new PdfDocument(
            "[NOT_USED]",
            Guid.NewGuid(),
            true,
            -1,
            outputService,
            pdfPigDocumentService,
            docnetAlternativeDocumentService,
            lookupConfiguration);
        
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
                    mockPdfDocument,
                    -1,
                    GeneralConstants.PdfPigDataExtractorServiceName)).ToList();

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

    static async Task<string?> GetTextResponseAsync(
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
                ConsoleHelper.WriteLine($"ERROR - Response truncated {chatResponse.Value?.FinishReason}");
                Console.Write(textResponse);

                return null;
            }

            if (!textResponse!.EndsWith('}'))
            {
                ConsoleHelper.WriteLine($"ERROR - Malformed JSON returned {chatResponse.Value?.FinishReason}");
                Console.Write(textResponse);

                return null;
            }
        }

        return textResponse;
    }

    static int GetMaxTokens(
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