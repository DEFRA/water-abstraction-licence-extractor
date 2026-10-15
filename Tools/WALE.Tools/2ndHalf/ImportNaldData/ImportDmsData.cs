using Dapper;
using Npgsql;
using NpgsqlTypes;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.Dms;
using WALE.ProcessFile.Database.PostgreSQL.Services;
using WALE.ProcessFile.Core.Constants;
using WALE.Tools.Config;
using WALE.Tools.Helpers;

namespace WALE.Tools._2ndHalf.ImportNaldData;

public static class ImportDmsData
{
    /// <param name="extractType">Which document type's extract this file is. Only that type's
    /// rows are replaced.</param>
    public static async Task<int> ImportAsync(string? extractType)
    {
        // Required rather than defaulted: an omitted type would replace the licence extract with
        // whichever file happens to be in the resources folder.
        if (extractType != DocumentTypes.AbstractionLicence && extractType != DocumentTypes.WrInspectionReport)
        {
            ConsoleHelper.WriteLine($"An extract type is required - '{DocumentTypes.AbstractionLicence}' "
                + $"or '{DocumentTypes.WrInspectionReport}'"
                + (string.IsNullOrWhiteSpace(extractType) ? "" : $" (given '{extractType}')"));

            return 1;
        }

        ConsoleHelper.WriteLine($"Starting {extractType} DMS data import into {KeyConfig.PostgresHost}...");

        var dmsRecords = GetDmsExtractsFromResourcesFolder();

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

        var records = dmsRecords.SelectMany(dmsFiles => dmsFiles.Value).ToList();
        ConsoleHelper.WriteLine($"{records.Count} records to insert");

        foreach (var dmsRecord in records)
        {
            dmsRecord.DocumentDate = ConvertExcelEpochToDateTimeString(dmsRecord.DocumentDate);
            dmsRecord.ModifiedDate = ConvertExcelEpochToDateTimeString(dmsRecord.ModifiedDate);
            dmsRecord.UploadDate = ConvertExcelEpochToDateTimeString(dmsRecord.UploadDate);
        }

        // The clear and the load share one transaction, so a failed import leaves the previous
        // extract in place rather than a half-filled table.
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await ClearExistingDmsExtractDataAsync(connection, extractType);
        var written = await CopyDmsRowsAsync(connection, records, extractType);

        // Checked before committing, so a short load rolls back rather than leaving the table
        // with fewer rows than the extract held.
        if (written != (ulong)records.Count)
        {
            throw new InvalidOperationException(
                $"COPY wrote {written} rows but {records.Count} were read from the extract. " +
                "Rolling back - dms_extract is unchanged.");
        }

        await transaction.CommitAsync();

        await SetDmsImportDateAsync(dataSource);

        ConsoleHelper.WriteLine($"{written} records inserted");
        ConsoleHelper.WriteLine("DMS extract import done");

        return 1;
    }

    /// <summary>
    /// Loads the rows with a single binary COPY rather than one INSERT each. Every column on
    /// dms_extract is text, so there is nothing to coerce.
    /// </summary>
    private static async Task<ulong> CopyDmsRowsAsync(
        NpgsqlConnection connection,
        IReadOnlyList<DmsExtract> records,
        string extractType)
    {
        await using var writer = await connection.BeginBinaryImportAsync(
            """
            COPY public.dms_extract (
                site_collection, library_name, permit_number, file_name, file_size, file_type,
                customer_operator_name, facility_name, facility_address,
                facility_address_postcode, regime, activity_class, activity_sub_class,
                type_of_permit, catchment, national_security, disclosure_status, document_date,
                upload_date, file_url, other_reference, modified_date, file_id, extract_type)
            FROM STDIN (FORMAT BINARY)
            """);

        var loopIndex = 0;

        foreach (var record in records)
        {
            await writer.StartRowAsync();

            foreach (var value in new[]
            {
                record.SiteCollection, record.LibraryName, record.PermitNumber, record.FileName,
                record.FileSize, record.FileType, record.CustomerOperatorName, record.FacilityName,
                record.FacilityAddress, record.FacilityAddressPostcode, record.Regime,
                record.ActivityClass, record.ActivitySubClass, record.TypeOfPermit,
                record.Catchment, record.NationalSecurity, record.DisclosureStatus,
                record.DocumentDate, record.UploadDate, record.FileUrl, record.OtherReference,
                record.ModifiedDate, record.FileId, extractType
            })
            {
                await writer.WriteAsync(value, NpgsqlDbType.Text);
            }

            loopIndex++;

            if (loopIndex % 50_000 == 0)
            {
                ConsoleHelper.WriteLine($"{loopIndex} records streamed so far");
            }
        }

        return await writer.CompleteAsync();
    }

    private static string ConvertExcelEpochToDateTimeString(string excelEpoch)
    {
        if (string.IsNullOrEmpty(excelEpoch) || excelEpoch.Contains('-'))
        {
            return excelEpoch;
        }

        if (excelEpoch.Contains('.'))
        {
            excelEpoch = excelEpoch.Split('.')[0];
        }

        var excelEpochInt = int.Parse(excelEpoch);
        var returnDate = DateTime.FromOADate(excelEpochInt);
            
        return returnDate.ToString("yyyy-MM-dd HH:mm:ss");
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
    
    /// <summary>Called only after the load commits, so the date never outruns the data.</summary>
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
    
    private static async Task ClearExistingDmsExtractDataAsync(
        NpgsqlConnection connection,
        string extractType) =>
        await connection.ExecuteAsync(
            "DELETE FROM public.dms_extract WHERE extract_type = @ExtractType;",
            new { ExtractType = extractType });
    
    /// <summary>
    /// Reads all files starting with 'Consolidated' from the resources folder
    /// </summary>
    /// <returns>Combined list of DMS extract records from all matching files</returns>
    private static Dictionary<string, List<DmsExtract>> GetDmsExtractsFromResourcesFolder()
    {
        var allDmsRecords = new Dictionary<string, List<DmsExtract>>(StringComparer.OrdinalIgnoreCase);
        var filepaths = FindFilesByPattern("Consolidated");
        
        Console.WriteLine($"Found {filepaths.Count} extract(s) - First is {filepaths.FirstOrDefault()}");
        
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
            if (dmsRecord.PermitNumber.Contains(andString, StringComparison.OrdinalIgnoreCase))
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
            .Replace("sandi", sString, StringComparison.OrdinalIgnoreCase)
            .Replace("sandg", sString, StringComparison.OrdinalIgnoreCase)
            .Replace("iands", sString, StringComparison.OrdinalIgnoreCase)
            .Replace("iandg", sString, StringComparison.OrdinalIgnoreCase);

        if (dmsRecord.PermitNumber.Contains(aAndBString, StringComparison.OrdinalIgnoreCase))
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
        
        if (dmsRecord.PermitNumber.Contains(andString, StringComparison.OrdinalIgnoreCase))
        {
            var parts = dmsRecord.PermitNumber.ToUpper().Split(andString);
            var firstPart  = parts[0];
            char splitChar;
            
            if (firstPart.Contains(gString, StringComparison.OrdinalIgnoreCase))
            {
                splitChar = gString[0];
            }
            else if (firstPart.Contains(iString, StringComparison.OrdinalIgnoreCase))
            {
                splitChar = iString[0];
            }
            else if (firstPart.Contains(sString, StringComparison.OrdinalIgnoreCase))
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
            .Where(filePath => filePath.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}