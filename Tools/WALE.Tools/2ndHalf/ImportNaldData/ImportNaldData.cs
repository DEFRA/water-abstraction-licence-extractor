using System.Globalization;
using System.Text;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Textract;
using Dapper;
using DocumentFormat.OpenXml.Office2010.ExcelAc;
using ICSharpCode.SharpZipLib.Zip;
using Npgsql;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Database.PostgreSQL.Services;
using WALE.Tools.Config;

namespace WALE.Tools._2ndHalf.ImportNaldData;

public static class ImportNaldData
{
    private static Dictionary<string, Dictionary<string, NpgsqlTypes.NpgsqlDbType>>? _schemaCache;
    private static List<ForeignKeyDefinition>? _foreignKeys;

    private static AmazonS3Client GetS3Client()
    {
        var awsCredentials = new BasicAWSCredentials(KeyConfig.AwsS3AccessKey, KeyConfig.AwsS3SecretKey);
        var client = new AmazonS3Client(awsCredentials, new AmazonS3Config
        {
            RegionEndpoint = RegionEndpoint.EUWest1
        });
        
        return client;
    }
    
    // Method to decompress files from a ZIP file
    private static void Decompress(string zipFilePath, string extractPath, string? password)
    {
        using var zipInputStream = new ZipInputStream(File.OpenRead(zipFilePath));

        if (!string.IsNullOrEmpty(password))
        {
            zipInputStream.Password = password;
        }

        // Read entries from the ZIP archive
        while (zipInputStream.GetNextEntry() is { } entry)
        {
            var entryPath = Path.Combine(extractPath, entry.Name);
            
            // Process files
            if (entry.IsFile)
            {
                using var fileStream = File.Create(entryPath);
                
                // Buffer for reading entries
                var buffer = new byte[4096];
                int bytesRead;
                
                // Read from ZIP stream and write to file
                while ((bytesRead = zipInputStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    fileStream.Write(buffer, 0, bytesRead);
                }
            }
            else if (entry.IsDirectory) // Process directories
            {
                Directory.CreateDirectory(entryPath);
            }
        }
    }

    public static async Task ImportAsync()
    {
        ConsoleHelper.WriteLine("Starting NALD data import...");
        var fileSource = "s3";

        var files = new List<string>();
        
        if (fileSource == "file")
        {
            var dumpFolder = KeyConfig.NaldDataDumpFolder;

            if (!Directory.Exists(dumpFolder))
            {
                ConsoleHelper.WriteLine($"Error: NALD dump folder not found at {dumpFolder}");
                return;
            }

            files.AddRange(Directory.GetFiles(dumpFolder, "NALD_*.txt"));
            ConsoleHelper.WriteLine($"Found {files.Count} files to import.");
        }
        else if (fileSource == "s3")
        {
            var client = GetS3Client();
            var response = await client.ListObjectsV2Async(
                new ListObjectsV2Request
                {
                    BucketName = KeyConfig.AwsS3BucketName
                });

            Directory.CreateDirectory("Temp");
            
            // There will actually only be one
            foreach (var s3Object in response.S3Objects)
            {
                var file = await client.GetObjectAsync(
                    new GetObjectRequest
                    {
                        BucketName = KeyConfig.AwsS3BucketName,
                        Key = s3Object.Key
                    });

                await file.WriteResponseStreamToFileAsync(
                    $"Temp/{s3Object.Key}",
                    false,
                    CancellationToken.None);
            }

            var folderPath = "Temp/wal_nald_data_release";
            
            var zipPath = $"{folderPath}/nald_enc.zip";
            Decompress(zipPath, "Temp",  KeyConfig.AwsS3ZipPassword);
            File.Delete(zipPath);
            Directory.Delete(folderPath);
            
            zipPath = "Temp/NALD.zip";
            Decompress(zipPath, "Temp", null);

            folderPath = "Temp/NALD";
            files = Directory.GetFiles(folderPath, "*.txt").ToList();
            
            File.Delete(zipPath);
        }
        else
        {
            throw new NotImplementedException($"NALD data import {fileSource} is not implemented yet.");
        }

        NpgsqlDataSourceProvider npgsqlDataSourceProvider = new(
            KeyConfig.PostgresHost,
            KeyConfig.PostgresPort,
            KeyConfig.PostgresDbName,
            KeyConfig.PostgresUsername,
            KeyConfig.PostgresPassword);

        await using var dataSource = npgsqlDataSourceProvider.DataSource;

        // Cache the schema information once
        await LoadSchemaAsync(dataSource);

        // Store foreign keys before dropping them
        await StoreForeignKeysAsync(dataSource);
        await DropForeignKeysAsync(dataSource);

        // Truncate all tables
        foreach (var filePath in files)
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            ConsoleHelper.WriteLine($"Truncating {fileName}...");

            try
            {
                await TruncateTableAsync(dataSource, fileName);
            }
            catch (Exception ex)
            {
                ConsoleHelper.WriteLine($"Error truncating {fileName}: {ex.Message}");
            }
        }

        // Import all files
        foreach (var filePath in files)
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            ConsoleHelper.WriteLine($"Importing {fileName}...");

            try
            {
                await ImportFileAsync(dataSource, filePath, fileName);
            }
            catch (Exception ex)
            {
                ConsoleHelper.WriteLine($"Error importing {fileName}: {ex.Message}");
            }
        }

        await RecreateForeignKeysAsync(dataSource);

        ConsoleHelper.WriteLine("NALD data import completed.");
    }

    private static async Task LoadSchemaAsync(NpgsqlDataSource dataSource)
    {
        _schemaCache = new Dictionary<string, Dictionary<string, NpgsqlTypes.NpgsqlDbType>>();

        var sql = @"
            SELECT 
                table_name as TableName,
                column_name as ColumnName,
                data_type as DataType,
                udt_name as UdtName
            FROM information_schema.columns
            WHERE table_schema = 'nald'
            ORDER BY table_name, ordinal_position";

        await using var connection = await dataSource.OpenConnectionAsync();
        var columns = await connection.QueryAsync<SchemaColumn>(sql);

        foreach (var column in columns)
        {
            if (!_schemaCache.ContainsKey(column.TableName))
            {
                _schemaCache[column.TableName] = new Dictionary<string, NpgsqlTypes.NpgsqlDbType>();
            }

            _schemaCache[column.TableName][column.ColumnName] = MapPostgresTypeToNpgsqlDbType(column.DataType, column.UdtName);
        }
    }

    private static async Task StoreForeignKeysAsync(NpgsqlDataSource dataSource)
    {
        ConsoleHelper.WriteLine("Storing foreign key definitions...");

        var sql = @"
            SELECT
                con.conname AS ConstraintName,
                rel.relname AS TableName,
                string_agg(att.attname, ', ' ORDER BY u.ord) AS ColumnNames,
                ref_rel.relname AS ReferencedTableName,
                string_agg(ref_att.attname, ', ' ORDER BY u.ord) AS ReferencedColumnNames,
                CASE con.confdeltype
                    WHEN 'a' THEN 'NO ACTION'
                    WHEN 'r' THEN 'RESTRICT'
                    WHEN 'c' THEN 'CASCADE'
                    WHEN 'n' THEN 'SET NULL'
                    WHEN 'd' THEN 'SET DEFAULT'
                END AS OnDeleteAction
            FROM pg_constraint con
            JOIN pg_class rel ON con.conrelid = rel.oid
            JOIN pg_namespace nsp ON rel.relnamespace = nsp.oid
            JOIN pg_class ref_rel ON con.confrelid = ref_rel.oid
            CROSS JOIN LATERAL unnest(con.conkey) WITH ORDINALITY AS u(attnum, ord)
            JOIN pg_attribute att ON att.attrelid = con.conrelid AND att.attnum = u.attnum
            CROSS JOIN LATERAL unnest(con.confkey) WITH ORDINALITY AS fu(fattnum, ford)
            JOIN pg_attribute ref_att ON ref_att.attrelid = con.confrelid AND ref_att.attnum = fu.fattnum
            WHERE con.contype = 'f'
                AND nsp.nspname = 'nald'
                AND u.ord = fu.ford
            GROUP BY con.conname, rel.relname, ref_rel.relname, con.confdeltype
            ORDER BY rel.relname, con.conname";

        await using var connection = await dataSource.OpenConnectionAsync();
        _foreignKeys = (await connection.QueryAsync<ForeignKeyDefinition>(sql)).ToList();

        ConsoleHelper.WriteLine($"Stored {_foreignKeys.Count} foreign key definitions.");
    }

    private static NpgsqlTypes.NpgsqlDbType MapPostgresTypeToNpgsqlDbType(string dataType, string udtName)
    {
        return dataType.ToLower() switch
        {
            "smallint" => NpgsqlTypes.NpgsqlDbType.Smallint,
            "integer" => NpgsqlTypes.NpgsqlDbType.Integer,
            "bigint" => NpgsqlTypes.NpgsqlDbType.Bigint,
            "numeric" => NpgsqlTypes.NpgsqlDbType.Numeric,
            "real" => NpgsqlTypes.NpgsqlDbType.Real,
            "double precision" => NpgsqlTypes.NpgsqlDbType.Double,
            "character varying" => NpgsqlTypes.NpgsqlDbType.Varchar,
            "character" => NpgsqlTypes.NpgsqlDbType.Char,
            "text" => NpgsqlTypes.NpgsqlDbType.Text,
            "timestamp without time zone" => NpgsqlTypes.NpgsqlDbType.Timestamp,
            "timestamp with time zone" => NpgsqlTypes.NpgsqlDbType.TimestampTz,
            "date" => NpgsqlTypes.NpgsqlDbType.Date,
            "boolean" => NpgsqlTypes.NpgsqlDbType.Boolean,
            _ => NpgsqlTypes.NpgsqlDbType.Text
        };
    }

    private static async Task WriteTypedValueAsync(NpgsqlBinaryImporter writer, string value,
        NpgsqlTypes.NpgsqlDbType type)
    {
        switch (type)
        {
            case NpgsqlTypes.NpgsqlDbType.Smallint:
                await writer.WriteAsync(short.Parse(value, CultureInfo.InvariantCulture), type);
                break;
            case NpgsqlTypes.NpgsqlDbType.Integer:
                await writer.WriteAsync(int.Parse(value, CultureInfo.InvariantCulture), type);
                break;
            case NpgsqlTypes.NpgsqlDbType.Bigint:
                await writer.WriteAsync(long.Parse(value, CultureInfo.InvariantCulture), type);
                break;
            case NpgsqlTypes.NpgsqlDbType.Numeric:
            case NpgsqlTypes.NpgsqlDbType.Real:
            case NpgsqlTypes.NpgsqlDbType.Double:
                await writer.WriteAsync(decimal.Parse(value, CultureInfo.InvariantCulture), type);
                break;
            case NpgsqlTypes.NpgsqlDbType.Timestamp:
            case NpgsqlTypes.NpgsqlDbType.TimestampTz:
            case NpgsqlTypes.NpgsqlDbType.Date:
                string[] formats =
                [
                    "dd/MM/yyyy",
                    "d/M/yyyy",
                    "yyyy-MM-dd",
                    "yyyy-MM-dd HH:mm:ss",
                    "yyyyMMddHHmmss",
                    "dd/MM/yyyy HH:mm:ss",
                    "d/M/yyyy HH:mm:ss"
                ];
                if (DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var result))
                {
                    await writer.WriteAsync(result, type);
                }
                else
                {
                    await writer.WriteAsync(DateTime.Parse(value, CultureInfo.InvariantCulture), type);
                }

                break;
            default:
                await writer.WriteAsync(value, type);
                break;
        }
    }

    private static async Task DropForeignKeysAsync(NpgsqlDataSource dataSource)
    {
        ConsoleHelper.WriteLine("Dropping all foreign keys in nald schema...");
        
        var sql = @"
            DO $$ 
            DECLARE 
                r RECORD;
            BEGIN
                FOR r IN (SELECT constraint_name, table_name 
                          FROM information_schema.table_constraints 
                          WHERE constraint_type = 'FOREIGN KEY' AND table_schema = 'nald') 
                LOOP
                    EXECUTE 'ALTER TABLE nald.' || quote_ident(r.table_name) || ' DROP CONSTRAINT ' || quote_ident(r.constraint_name);
                END LOOP;
            END $$;";

        await using var connection = await dataSource.OpenConnectionAsync();
        await connection.ExecuteAsync(sql);
    }

    private static async Task RecreateForeignKeysAsync(NpgsqlDataSource dataSource)
    {
        if (_foreignKeys == null || _foreignKeys.Count == 0)
        {
            ConsoleHelper.WriteLine("No foreign keys to recreate.");
            return;
        }

        ConsoleHelper.WriteLine($"Re-creating {_foreignKeys.Count} foreign keys...");

        await using var connection = await dataSource.OpenConnectionAsync();
        foreach (var fk in _foreignKeys)
        {
            try
            {
                var onDeleteClause = fk.OnDeleteAction.ToUpper() switch
                {
                    "CASCADE" => " ON DELETE CASCADE",
                    "SET NULL" => " ON DELETE SET NULL",
                    "SET DEFAULT" => " ON DELETE SET DEFAULT",
                    "RESTRICT" => " ON DELETE RESTRICT",
                    _ => "" // NO ACTION is the default
                };

                var sql = $@"ALTER TABLE nald.""{fk.TableName}"" 
                    ADD CONSTRAINT ""{fk.ConstraintName}"" 
                    FOREIGN KEY ({string.Join(", ", fk.ColumnNames.Split(", ").Select(c => $"\"{c}\""))}) 
                    REFERENCES nald.""{fk.ReferencedTableName}"" ({string.Join(", ", fk.ReferencedColumnNames.Split(", ").Select(c => $"\"{c}\""))}){onDeleteClause};";

                await connection.ExecuteAsync(sql);
                ConsoleHelper.WriteLine($"Recreated FK: {fk.ConstraintName} on {fk.TableName}");
            }
            catch (Exception ex)
            {
                ConsoleHelper.WriteLine($"Error recreating FK {fk.ConstraintName} on {fk.TableName}: {ex.Message}");
            }
        }

        ConsoleHelper.WriteLine("Foreign key recreation completed.");
    }

    private static async Task TruncateTableAsync(NpgsqlDataSource dataSource, string tableName)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await connection.ExecuteAsync($"TRUNCATE TABLE nald.\"{tableName}\" CASCADE");
    }

    private static async Task ImportFileAsync(NpgsqlDataSource dataSource, string filePath, string tableName)
    {
        using var reader = new StreamReader(filePath, Encoding.UTF8);
        var headerLine = await reader.ReadLineAsync();

        if (headerLine == null)
        {
            return;
        }

        // Strip BOM if present
        if (headerLine.StartsWith('\uFEFF'))
        {
            headerLine = headerLine.Substring(1);
        }

        var columns = headerLine.Split(',').Select(c => c.Trim()).ToArray();

        // Filter out columns not in our schema (specifically SOURCE_CODE and BATCH_RUN_DATE)
        var columnsToImport = columns.Where(c => c != "SOURCE_CODE" && c != "BATCH_RUN_DATE").ToArray();
        var columnNames = string.Join(", ", columnsToImport.Select(c => $"\"{c}\""));

        // For binary import, we still need to open a connection
        await using var connection = await dataSource.OpenConnectionAsync();

        await using var writer = await connection.BeginBinaryImportAsync(
            $"COPY nald.\"{tableName}\" ({columnNames}) FROM STDIN (FORMAT BINARY)");

        var values = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        var charBuffer = new char[1];

        while (await reader.ReadAsync(charBuffer, 0, 1) > 0)
        {
            var c = charBuffer[0];
            if (c == '\"')
            {
                inQuotes = !inQuotes;
                // Handle escaped quotes (double-double quotes)
                if (!inQuotes) // We just closed quotes, check if next is also quote
                {
                    int next = reader.Peek();
                    if (next == '\"')
                    {
                        current.Append('\"');
                        await reader.ReadAsync(charBuffer, 0, 1); // consume the second quote
                        inQuotes = true; // still in quotes
                    }
                }
            }
            else if (c == ',' && !inQuotes)
            {
                values.Add(current.ToString());
                current.Clear();
            }
            else if ((c == '\n' || c == '\r') && !inQuotes)
            {
                if (c == '\r' && reader.Peek() == '\n')
                {
                    await reader.ReadAsync(charBuffer, 0, 1);
                }

                if (values.Count > 0 || current.Length > 0)
                {
                    values.Add(current.ToString());
                    await WriteRowAsync(writer, values, columns, columnsToImport, tableName);
                    values.Clear();
                    current.Clear();
                }
            }
            else
            {
                current.Append(c);
            }
        }

        // Handle last row if file doesn't end with newline
        if (values.Count > 0 || current.Length > 0)
        {
            values.Add(current.ToString());
            await WriteRowAsync(writer, values, columns, columnsToImport, tableName);
        }

        await writer.CompleteAsync();
    }

    private static async Task WriteRowAsync(NpgsqlBinaryImporter writer, List<string> values,
        string[] allColumnsInFile, string[] columnsToImport, string tableName)
    {
        if (values.Count != allColumnsInFile.Length)
        {
            if (values.Count > 1 || !string.IsNullOrWhiteSpace(values[0]))
            {
                ConsoleHelper.WriteLine(
                    $"Warning: Line in {tableName} has {values.Count} columns, expected {allColumnsInFile.Length}. Skipping.");
            }

            return;
        }

        await writer.StartRowAsync();

        for (int i = 0; i < values.Count; i++)
        {
            var columnName = allColumnsInFile[i];
            if (columnName == "SOURCE_CODE" || columnName == "BATCH_RUN_DATE")
            {
                continue;
            }

            var value = values[i];

            if (string.IsNullOrEmpty(value) || value == "null")
            {
                await writer.WriteNullAsync();
                continue;
            }

            if (_schemaCache!.TryGetValue(tableName, out var tableSchema) &&
                tableSchema.TryGetValue(columnName, out var npgsqlType))
            {
                try
                {
                    await WriteTypedValueAsync(writer, value, npgsqlType);
                }
                catch (Exception ex)
                {
                    ConsoleHelper.WriteLine(
                        $"Error parsing value '{value}' for column '{columnName}' in table '{tableName}' as type '{npgsqlType}': {ex.Message}. Writing as null.");
                    await writer.WriteNullAsync();
                }
            }
            else
            {
                await writer.WriteAsync(value, NpgsqlTypes.NpgsqlDbType.Text);
            }
        }
    }
}