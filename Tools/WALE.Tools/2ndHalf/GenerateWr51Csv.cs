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
using WALE.ProcessFile.Services.Output;
using WALE.ProcessFile.Services.PdfPig;
using WALE.ProcessFile.Services.Services;
using WALE.Tools.Helpers;
using WALE.Tools.Models;

namespace WALE.Tools._2ndHalf;

public static class GenerateWr51Csv
{
    private static readonly ICacheService CacheService = new FileSystemCacheService("Cache/");
    private static readonly IOutputService OutputService = new FileSystemOutputService("Output/");
    private static readonly INoOcrPdfDocumentService DocumentService = new PdfPigNoOcrPdfDocumentService();
    private static readonly INoOcrAlternativePdfDocumentService DocnetAlternativeDocumentService =
        new DocnetNoOcrAlternativePdfDocumentService();
    
    private static readonly IPdfDataExtractorService PdfDataExtractor = new PdfDataExtractorService(
        new PdfPigNoOcrDataExtractorService(),
        new List<IOcrDataExtractorService>
        {
            // TODO mock of an OCR service that errors if called
        },
        CacheService,
        OutputService,
        DocumentService,
        DocnetAlternativeDocumentService);
    
    public static async Task<int> GenerateCsvAsync()
    {
        var folderPath = "/Users/ryanbarlow/Downloads/WR51s/";
        ConsoleHelper.WriteLine("Started generating wr51s csv");
        
        var filesInDirectory = Directory.GetFiles(folderPath);
        var uniqueFolder = $"WR51-{DateTime.Today:yyyyMMdd}";
        
        await using var writer = new StreamWriter(
            $"{uniqueFolder}.csv",
            false,
            Encoding.Unicode);
        
        await using var csv = new CsvWriter(writer, new CultureInfo("en-GB"));

        var lines = new List<Wr51CsvLine>();
        
        foreach (var filepath in filesInDirectory)
        {
            var internalResults = await GetMatchesAsync(filepath, folderPath);
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
        
        await ZipFile.CreateFromDirectoryAsync("Images", $"{uniqueFolder}-images.zip");
        
        await csv.WriteRecordsAsync((IEnumerable)lines);
        ConsoleHelper.WriteLine("Finished generating wr51s csv");
        
        return 1;
    }
    
    private static Task<MatchesResult> GetMatchesAsync(string filepath, string folderPath)
    {
        var fileName = Path.GetFileName(filepath);
        
        return PdfDataExtractor.GetMatchesAsync(
            fileName,
            new DmsFileData { FileId = GuidHelper.GetConsistentFileIdFromFilename(fileName) },
            LookupConfiguration(folderPath),
            [fileName],
            -1);
    }
    
    private static LookupConfiguration LookupConfiguration(string pdfFolder)
    {
        return new LookupConfiguration(
            Wr51LabelConfiguration.GetLabels(),
            [],
            [],
            [],
            new LocalFileService(pdfFolder),
            CacheService,
            GeneralConstants.UnsetRegionCode,
            lineHeight: 6,
            minimumRowsForDigital: 40);
    }
}