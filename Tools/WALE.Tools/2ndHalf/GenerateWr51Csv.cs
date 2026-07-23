using System.Collections;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using CsvHelper;
using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Services.Cache;
using WALE.ProcessFile.Services.Configuration;
using WALE.ProcessFile.Services.Converters;
using WALE.ProcessFile.Services.Docnet;
using WALE.ProcessFile.Services.Helpers;
using WALE.ProcessFile.Services.Output;
using WALE.ProcessFile.Services.PdfPig;
using WALE.ProcessFile.Services.Services;
using WALE.Tools.Config;
using WALE.Tools.Models;

namespace WALE.Tools._2ndHalf;

public static class GenerateWr51Csv
{
    private static readonly INoOcrPdfDocumentService DocumentService = new PdfPigNoOcrPdfDocumentService();
    private static readonly INoOcrAlternativePdfDocumentService DocnetAlternativeDocumentService =
        new DocnetNoOcrAlternativePdfDocumentService();

    private static IPdfDataExtractorService GetPdfDataExtractor(
        ICacheService cacheService,
        IOutputService outputService,
        IMessageQueueService messageQueueService)
    {
        return new PdfDataExtractorService(
            new PdfPigNoOcrDataExtractorService(),
            new List<IOcrDataExtractorService>(),
            cacheService,
            outputService,
            DocumentService,
            DocnetAlternativeDocumentService,
            messageQueueService);
    }

    public static async Task<int> GenerateCsvAsync()
    {
        ConsoleHelper.WriteLine("Started generating wr51s csv");
        
        var httpClient = HttpHelper.GetResilientHttpClient(
            KeyConfig.ApiBaseUrl,
            100,
            30);
        
        ICacheService cacheService = new ApiCacheService(httpClient);
        IOutputService outputService = new ApiOutputService(httpClient);
        IMessageQueueService messageQueueService  = new ApiMessageQueueService(httpClient);
        var pdfDataExtractor = GetPdfDataExtractor(cacheService, outputService, messageQueueService);
        
        const bool useS3Api = true;
        List<string> files;
        IFileService fileService;
        
        if (useS3Api)
        {
            fileService = new ApiFileService(httpClient);
            files = (await fileService.GetAllFilesAsync())
                .Where(f => f.StartsWith("wr51__", StringComparison.InvariantCultureIgnoreCase))
                .ToList();
        }
        else
        {
            var folderPath = "/Users/ryanbarlow/Downloads/WR51s/";
            files = Directory.GetFiles(folderPath).ToList();
            
            fileService = new LocalFileService(folderPath);
        }

        files = files.Take(10).ToList();
        
        var lookupConfiguration = LookupConfiguration(fileService, cacheService, outputService);
        
        var uniqueFolder = $"WR51-{DateTime.Today:yyyyMMdd}";
        
        await using var writer = new StreamWriter(
            $"{uniqueFolder}.csv",
            false,
            Encoding.Unicode);
        
        await using var csv = new CsvWriter(writer, new CultureInfo("en-GB"));
        var lines = new List<Wr51CsvLine>();

        const int processRunId = -99;
        
        foreach (var filepath in files)
        {
            var fileName = Path.GetFileName(filepath);

            var fileId = FileHelper.ExtractFileId(fileName);
            if (fileId == null)
            {
                ConsoleHelper.WriteLine($"ERROR - {fileName} doesn't contain a fileid guid");
                continue;
            }
            
            var (_, alreadySaved, internalResults) = await GetMatchesAsync(
                fileName,
                fileId.Value,
                lookupConfiguration,
                pdfDataExtractor,
                processRunId);

            if (internalResults == null)
            {
                continue;
            }
            
            if (alreadySaved == false)
            {
                await pdfDataExtractor.SaveMatchResultAsync(
                    internalResults,
                    fileId.Value,
                    processRunId,
                    lookupConfiguration.UseLockExclusivity);
            }
            
            var parsedForm = Wr51SchemaConverter.ToForm(internalResults);

            if (parsedForm.Images.Count > 0)
            {
                var firstImage = parsedForm.Images.First();
                var pathParts = firstImage.Split('/');
                var folderName = pathParts[0];
                
                Directory.CreateDirectory($"Images/{folderName}");

                foreach (var image in parsedForm.Images)
                {
                    var filenameParts = Path.GetFileNameWithoutExtension(image).Split('-');
                    var serviceName = filenameParts[0];
                    var pageNumber = int.Parse(filenameParts[1].Replace("page", string.Empty));
                    var imageNumber = int.Parse(filenameParts[2].Replace("image", string.Empty));

                    var outputFolder = $"{parsedForm.Metadata.FileId}/{serviceName}/Images";
                    var partialOutputFilename = $"page-{pageNumber}-image-{imageNumber}";

                    var sourceImages = Directory.GetFiles($"Cache/{outputFolder}");
                    var sourceImage = sourceImages
                        .Select(Path.GetFileName)
                        .Single(f => f!.StartsWith(partialOutputFilename, StringComparison.InvariantCultureIgnoreCase));

                    var sourceFullPath = sourceImages.Single(i => i.Contains(sourceImage!, StringComparison.InvariantCultureIgnoreCase));
                    var destinationFullPath = $"Images/{image}";
                    
                    File.Copy(sourceFullPath, destinationFullPath, true);
                }
            }

            var imagesSb = new StringBuilder();

            foreach (var image in parsedForm.Images)
            {
                if (imagesSb.Length > 0)
                {
                    imagesSb.Append('\n');
                }
                
                imagesSb.Append(image);
            }
            
            lines.Add(new Wr51CsvLine
            {
                Metadata__Filename = parsedForm.Metadata.Filename,
                Metadata__FormSentTo = parsedForm.Metadata.FormSentTo,
                Metadata__DocumentTemplateVerison =  parsedForm.Metadata.DocumentTemplateVerison,
                Metadata__IsScan =  parsedForm.Metadata.IsScan,
                Metadata__Date__Date = parsedForm.Metadata.Date.Date?.ToString("dd/MM/yyyy"),
                Metadata__Date__RawDate = parsedForm.Metadata.Date.RawDate,
                LicenceNumber = parsedForm.LicenceNumber,
                InspectionClass =  parsedForm.InspectionClass,
                InspectingOfficer =   parsedForm.InspectingOfficer,
                GeneralComments =  parsedForm.GeneralComments,
                Images = imagesSb.Length > 0 ? imagesSb.ToString() : null,
                Address__NameAndAddress = parsedForm.Address.NameAndAddress,
                Address__SiteAddress = parsedForm.Address.SiteAddress,
                Address__TelephoneNumber = parsedForm.Address.TelephoneNumber,
                MetWith__Name = parsedForm.MetWith.Name,
                MetWith__Position = parsedForm.MetWith.Position,
                InspectionDate__DateTime = parsedForm.InspectionDate.DateTime?.ToString("dd/MM/yyyy HH:mm:ss"),
                InspectionDate__RawDate = parsedForm.InspectionDate.RawDate,
                InspectionDate__RawTime = parsedForm.InspectionDate.RawTime,
                LicenceProvisions__SourceOfSupply = parsedForm.LicenceProvisions.SourceOfSupply.ToString(),
                LicenceProvisions__Purposes = parsedForm.LicenceProvisions.Purposes.ToString(),
                LicenceProvisions__PointOfAbstraction = parsedForm.LicenceProvisions.PointOfAbstraction.ToString(),
                LicenceProvisions__SpecialConditions = parsedForm.LicenceProvisions.SpecialConditions.ToString(),
                LicenceProvisions__MeansOfAbstraction = parsedForm.LicenceProvisions.MeansOfAbstraction.ToString(),
                LicenceProvisions__Period = parsedForm.LicenceProvisions.Period.ToString(),
                LicenceProvisions__Quantities = parsedForm.LicenceProvisions.Quantities.ToString(),
                LicenceProvisions__MeansOfMeasurement = parsedForm.LicenceProvisions.MeansOfMeasurement.ToString(),
                LicenceProvisions__Records = parsedForm.LicenceProvisions.Records.ToString(),
                LicenceProvisions__ProvisionOfInformation = parsedForm.LicenceProvisions.ProvisionOfInformation.ToString(),
                LicenceProvisions__Land = parsedForm.LicenceProvisions.Land.ToString(),
                LicenceProvisions__ChargingFactors = parsedForm.LicenceProvisions.ChargingFactors.ToString(),
                LicenceProvisions__OtherProvisions = parsedForm.LicenceProvisions.OtherProvisions.ToString(),
                MeasurementDetails__MeterMake = parsedForm.MeasurementDetails.MeterMake,
                MeasurementDetails__SerialNumber = parsedForm.MeasurementDetails.SerialNumber,
                MeasurementDetails__Reading = parsedForm.MeasurementDetails.Reading,
                MeasurementDetails__Units = parsedForm.MeasurementDetails.Units,
                MeasurementDetails__Other = parsedForm.MeasurementDetails.Other,
                MeasurementDetails__CertificatesOrRecordsAvailableFor = parsedForm.MeasurementDetails.CertificatesOrRecordsAvailableFor,
                MeasurementDetails__DateOfCertificateOrRecord__Date = parsedForm.MeasurementDetails.DateOfCertificateOrRecord.Date?.ToString("dd/MM/yyyy"),
                MeasurementDetails__DateOfCertificateOrRecord__RawDate = parsedForm.MeasurementDetails.DateOfCertificateOrRecord.RawDate,
                MeasurementDetails__Calibration = parsedForm.MeasurementDetails.Calibration,
                MeasurementDetails__Conformance = parsedForm.MeasurementDetails.Conformance,
                MeasurementDetails__FlowVerification = parsedForm.MeasurementDetails.FlowVerification,
                MeasurementDetails__MeterVerification = parsedForm.MeasurementDetails.MeterVerification,
                MeasurementDetails__Maintenance__ByWhom = parsedForm.MeasurementDetails.Maintenance.ByWhom,
                MeasurementDetails__Maintenance__Maintenance = parsedForm.MeasurementDetails.Maintenance.Maintenance,
                MeasurementDetails__Maintenance__Frequency = parsedForm.MeasurementDetails.Maintenance.Frequency,
                MeasurementDetails__ReadingsTaken__ByWhom = parsedForm.MeasurementDetails.ReadingsTaken.ByWhom,
                MeasurementDetails__ReadingsTaken__ReadingsTaken = parsedForm.MeasurementDetails.ReadingsTaken.ReadingsTaken,
                MeasurementDetails__ReadingsTaken__Frequency = parsedForm.MeasurementDetails.ReadingsTaken.Frequency,
                MeasurementDetails__WhereKept = parsedForm.MeasurementDetails.WhereKept
            });
        }

        var zipFileName = $"{uniqueFolder}-images.zip";

        if (File.Exists(zipFileName))
        {
            File.Delete(zipFileName);
        }

        await ZipFile.CreateFromDirectoryAsync("Images", zipFileName);
        
        await csv.WriteRecordsAsync((IEnumerable)lines);
        ConsoleHelper.WriteLine("Finished generating wr51s csv");
        
        return 1;
    }
    
    private static Task<(bool StopExecution, bool? AlreadySaved, MatchesResult? Item)> GetMatchesAsync(
        string fileName,
        Guid fileId,
        LookupConfiguration lookupConfiguration,
        IPdfDataExtractorService pdfDataExtractor,
        int processRunId)
    {
        return pdfDataExtractor.GetMatchesAsync(
            fileName,
            new DmsFileData { FileId = fileId },
            lookupConfiguration,
            [fileName],
            processRunId);
    }
    
    private static LookupConfiguration LookupConfiguration(
        IFileService fileService,
        ICacheService cacheService,
        IOutputService outputService)
    {
        return new LookupConfiguration(
            Wr51LabelConfiguration.GetLabels(),
            [],
            fileService,
            cacheService,
            outputService,
            GeneralConstants.UnsetRegionCode,
            DateTime.Now,
            lineHeight: 6,
            minimumRowsForDigital: 40);
    }
}