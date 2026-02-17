using System.Globalization;
using System.Text;
using Dapper;
using Npgsql;
using WALE.ProcessFile.Database.PostgreSQL.Services;
using WALE.Tools.Config;

namespace WALE.Tools._2ndHalf;

public static class ImportNaldData
{
    private static Dictionary<string, Dictionary<string, NpgsqlTypes.NpgsqlDbType>>? _schemaCache;
    private static List<ForeignKeyDefinition>? _foreignKeys;

    private class ForeignKeyDefinition
    {
        public string ConstraintName { get; set; } = string.Empty;
        public string TableName { get; set; } = string.Empty;
        public string ColumnNames { get; set; } = string.Empty;
        public string ReferencedTableName { get; set; } = string.Empty;
        public string ReferencedColumnNames { get; set; } = string.Empty;
        public string OnDeleteAction { get; set; } = string.Empty;
    }

    private class SchemaColumn
    {
        public string TableName { get; set; } = string.Empty;
        public string ColumnName { get; set; } = string.Empty;
        public string DataType { get; set; } = string.Empty;
        public string UdtName { get; set; } = string.Empty;
    }

    public static async Task ImportAsync()
    {
        Console.WriteLine("Starting NALD data import...");
        var dumpFolder = KeyConfig.NaldDataDumpFolder;

        if (!Directory.Exists(dumpFolder))
        {
            Console.WriteLine($"Error: NALD dump folder not found at {dumpFolder}");
            return;
        }

        var files = Directory.GetFiles(dumpFolder, "NALD_*.txt");
        Console.WriteLine($"Found {files.Length} files to import.");

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
            Console.WriteLine($"Truncating {fileName}...");

            try
            {
                await TruncateTableAsync(dataSource, fileName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error truncating {fileName}: {ex.Message}");
            }
        }

        // Import all files
        foreach (var filePath in files)
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            Console.WriteLine($"Importing {fileName}...");

            try
            {
                await ImportFileAsync(dataSource, filePath, fileName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error importing {fileName}: {ex.Message}");
            }
        }

        await RecreateForeignKeysAsync(dataSource);

        Console.WriteLine("NALD data import completed.");
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
        Console.WriteLine("Storing foreign key definitions...");

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

        Console.WriteLine($"Stored {_foreignKeys.Count} foreign key definitions.");
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
        Console.WriteLine("Dropping all foreign keys in nald schema...");
        
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
            Console.WriteLine("No foreign keys to recreate.");
            return;
        }

        Console.WriteLine($"Re-creating {_foreignKeys.Count} foreign keys...");

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
                Console.WriteLine($"Recreated FK: {fk.ConstraintName} on {fk.TableName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error recreating FK {fk.ConstraintName} on {fk.TableName}: {ex.Message}");
            }
        }

        Console.WriteLine("Foreign key recreation completed.");
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
                Console.WriteLine(
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
                    Console.WriteLine(
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