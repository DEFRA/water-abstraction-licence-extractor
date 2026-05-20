using Dapper;
using Npgsql;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Database.PostgreSQL.Services;
using WALE.Tools.Config;
using WALE.Tools.Helpers;

namespace WALE.Tools._2ndHalf.ImportNaldData;

public static class ImportDmsData
{
    public static async Task<int> ImportAsync()
    {
        ConsoleHelper.WriteLine("Starting DMS data import...");

        var dmsRecords = GetDmsExtracts();

        if (dmsRecords.Count == 0)
        {
            ConsoleHelper.WriteLine("No DMS extract found...");
            return 1;
        }
        
        NpgsqlDataSourceProvider npgsqlDataSourceProvider = new(
            KeyConfig.PostgresHost,
            KeyConfig.PostgresPort,
            KeyConfig.PostgresDbName,
            KeyConfig.PostgresUsername,
            KeyConfig.PostgresPassword);

        await using var dataSource = npgsqlDataSourceProvider.DataSource;
        
        await ClearExistingDmsExtractDataAsync(dataSource);
        await SetDmsImportDateAsync(dataSource);

        var count = dmsRecords.Sum(dmsFiles => dmsFiles.Value.Count);
        ConsoleHelper.WriteLine($"{count} records to insert");

        var loopIndex = 0;
        
        foreach (var dmsRecord in dmsRecords.SelectMany(
            dmsFiles => dmsFiles.Value))
        {
            await InsertDmsRow(dataSource, dmsRecord);

            if (loopIndex++ % 1000 == 0)
            {
                ConsoleHelper.WriteLine($"{loopIndex} records inserted so far");
            }
        }
        
        ConsoleHelper.WriteLine("DMS extract import done");
        return 1;
    }
    
    private static async Task InsertDmsRow(NpgsqlDataSource dataSource, DmsExtract dmsRecord)
    {
        var sql = @"
            INSERT INTO public.dms_extract (
                site_collection,
                library_name,
                permit_number,
                file_name,
                file_size,
                file_type,
                customer_operator_name,
                facility_name,
                facility_address,
                facility_address_postcode,
                regime,
                activity_class,
                activity_sub_class,
                type_of_permit,
                catchment,
                national_security,
                disclosure_status,
                document_date,
                upload_date,
                file_url,
                other_reference,
                modified_date,
                file_id)
            VALUES (
                @SiteCollection,
                @LibraryName,
                @PermitNumber,
                @FileName,
                @FileSize,
                @FileType,
                @CustomerOperatorName,
                @FacilityName,
                @FacilityAddress,
                @FacilityAddressPostcode,
                @Regime,
                @ActivityClass,
                @ActivitySubClass,
                @TypeOfPermit,
                @Catchment,
                @NationalSecurity,
                @DisclosureStatus,
                @DocumentDate,
                @UploadDate,
                @FileUrl,
                @OtherReference,
                @ModifiedDate,
                @FileId)";

        await using var connection = await dataSource.OpenConnectionAsync();

        await connection.ExecuteAsync(sql, new
        {
            dmsRecord.SiteCollection,
            dmsRecord.LibraryName,
            dmsRecord.PermitNumber,
            dmsRecord.FileName,
            dmsRecord.FileSize,
            dmsRecord.FileType,
            dmsRecord.CustomerOperatorName,
            dmsRecord.FacilityName,
            dmsRecord.FacilityAddress,
            dmsRecord.FacilityAddressPostcode,
            dmsRecord.Regime,
            dmsRecord.ActivityClass,
            dmsRecord.ActivitySubClass,
            dmsRecord.TypeOfPermit,
            dmsRecord.Catchment,
            dmsRecord.NationalSecurity,
            dmsRecord.DisclosureStatus,
            dmsRecord.DocumentDate,
            dmsRecord.UploadDate,
            dmsRecord.FileUrl,
            dmsRecord.OtherReference,
            dmsRecord.ModifiedDate,
            dmsRecord.FileId
        });
    }
    
    private static async Task SetDmsImportDateAsync(NpgsqlDataSource dataSource)
    {
        var sql = @"
            INSERT INTO public.import_dates (data_source, date_time)
            VALUES (@DataSource, @DateTime)";

        await using var connection = await dataSource.OpenConnectionAsync();

        await connection.ExecuteAsync(sql, new
        {
            DataSource = "DmsExtract",
            DateTime = DateTime.Now
        });
    }
    
    private static async Task ClearExistingDmsExtractDataAsync(NpgsqlDataSource dataSource)
    {
        var sql = "DELETE FROM public.dms_extract;";

        await using var connection = await dataSource.OpenConnectionAsync();
        await connection.ExecuteAsync(sql);
    }
    
    /// <summary>
    /// Reads all files starting with 'Consolidated' from the resources folder
    /// </summary>
    /// <returns>Combined list of DMS extract records from all matching files</returns>
    private static Dictionary<string, List<DmsExtract>> GetDmsExtracts()
    {
        var allDmsRecords = new Dictionary<string, List<DmsExtract>>(StringComparer.OrdinalIgnoreCase);
        var filepaths = FindFilesByPattern("Consolidated");
        var fileProcessor = new LicenceFileProcessor();
        
        foreach (var filepath in filepaths)
        {
            var records = fileProcessor.ExtractExcel<List<DmsExtract>>(
                filepath,
                new Dictionary<string, List<string>>
                {
                    {"Site Collection", ["SiteCollection"]},
                    {"Permit Number", ["PermitNumber"]},
                    {"Document Date", ["DocumentDate"]},
                    {"Uploaded Date", ["UploadDate"]},
                    {"File URL", ["FileUrl"]},
                    {"File Name", ["FileName"]},
                    {"File Size", ["FileSize"]},
                    {"Disclosure Status", ["DisclosureStatus"]},
                    {"Other Reference", ["OtherReference"]},
                    {"Modified Date", ["ModifiedDate"]},
                    {"File ID", ["FileId"]},
                    {"ETag", ["FileId"]}
                },
                [
                    "ActivityGrouping",
                    "EPRNumber",
                    "CurrentPermit",
                    "FileID_wrong"
                ]);

            foreach (var record in records)
            {
                var completed = AddToListFoldersWithWordAndIn(record, allDmsRecords);
                if (completed)
                {
                    continue;
                }
                
                if (allDmsRecords.TryGetValue(record.PermitNumber, out var list))
                {
                    list.Add(record);
                    continue;
                }
                
                allDmsRecords.Add(record.PermitNumber, [record]);
            }
        }

        return allDmsRecords;
    }
    
    private static bool AddToListFoldersWithWordAndIn(
        DmsExtract dmsRecord,
        Dictionary<string, List<DmsExtract>> allDmsRecords)
    {
        const string andString = "AND";
        var dontWorkAroundCombinedDmsFolders = true;

        if (dontWorkAroundCombinedDmsFolders)
        {
            if (dmsRecord.PermitNumber.Contains(andString, StringComparison.InvariantCultureIgnoreCase))
            {
                // Don't add anything
                return true;
            }

            return false;
        }

        const string sString = "S";
        const string gString = "G";
        const string iString = "I";
        const string aAndBString = "AANDB";
        
        dmsRecord.PermitNumber = dmsRecord.PermitNumber
            .Replace("sandi", sString, StringComparison.InvariantCultureIgnoreCase)
            .Replace("sandg", sString, StringComparison.InvariantCultureIgnoreCase)
            .Replace("iands", sString, StringComparison.InvariantCultureIgnoreCase)
            .Replace("iandg", sString, StringComparison.InvariantCultureIgnoreCase);

        if (dmsRecord.PermitNumber.Contains(aAndBString, StringComparison.InvariantCultureIgnoreCase))
        {
            var parts = dmsRecord.PermitNumber.ToUpper().Split(andString);
            dmsRecord.PermitNumber = parts[0];
            
            if (allDmsRecords.TryGetValue(dmsRecord.PermitNumber, out var listA))
            {
                listA.Add(dmsRecord);
            }
            else
            {
                allDmsRecords.Add(dmsRecord.PermitNumber, [dmsRecord]);
            }

            var clonedDmsRecord = dmsRecord.Clone();
            clonedDmsRecord.PermitNumber = clonedDmsRecord.PermitNumber[..^1] + 'B';
            
            if (allDmsRecords.TryGetValue(clonedDmsRecord.PermitNumber, out var listB))
            {
                listB.Add(clonedDmsRecord);
            }
            else
            {
                allDmsRecords.Add(clonedDmsRecord.PermitNumber, [clonedDmsRecord]);
            }
            
            return true;
        }
        
        if (dmsRecord.PermitNumber.Contains(andString, StringComparison.InvariantCultureIgnoreCase))
        {
            var parts = dmsRecord.PermitNumber.ToUpper().Split(andString);
            var firstPart  = parts[0];
            char splitChar;
            
            if (firstPart.Contains(gString, StringComparison.InvariantCultureIgnoreCase))
            {
                splitChar = gString[0];
            }
            else if (firstPart.Contains(iString, StringComparison.InvariantCultureIgnoreCase))
            {
                splitChar = iString[0];
            }
            else if (firstPart.Contains(sString, StringComparison.InvariantCultureIgnoreCase))
            {
                splitChar = sString[0];
            }
            else
            {
                throw new NotImplementedException();
            }
            
            var beforeAndAfterLicenceChar = firstPart.Split(splitChar);
            var sharedPart = beforeAndAfterLicenceChar[0] + splitChar;

            var suffix1 = beforeAndAfterLicenceChar[1];
            dmsRecord.PermitNumber = $"{sharedPart}{suffix1}";
            
            if (allDmsRecords.TryGetValue(dmsRecord.PermitNumber, out var list0))
            {
                list0.Add(dmsRecord);
            }
            else
            {
                allDmsRecords.Add(dmsRecord.PermitNumber, [dmsRecord]);
            }

            foreach (var suffix in parts.Skip(1))
            {
                var clonedDmsRecord = dmsRecord.Clone();
                clonedDmsRecord.PermitNumber = $"{sharedPart}{suffix}";
                
                if (allDmsRecords.TryGetValue(clonedDmsRecord.PermitNumber, out var list1))
                {
                    list1.Add(clonedDmsRecord);
                    continue;
                }
        
                allDmsRecords.Add(clonedDmsRecord.PermitNumber, [clonedDmsRecord]);
            }
            
            return true;
        }

        return false;
    }
    
    private static List<string> FindFilesByPattern(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            throw new ArgumentException("Pattern cannot be null or empty", nameof(pattern));
        }

        var matchingFiles = Directory.GetFiles("../../../Resources").ToList();
        
        return matchingFiles
            .Distinct()
            .Where(filePath => filePath.Contains(pattern, StringComparison.InvariantCultureIgnoreCase))
            .ToList();
    }
}