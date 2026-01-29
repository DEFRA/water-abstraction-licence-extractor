using System.Text;
using Npgsql;
using WALE.ProcessFile.Database.PostgreSQL.Services;
using WALE.Tools.Config;

namespace WALE.Tools;

public static class ImportNaldData
{
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

        Console.WriteLine("NALD data import completed.");
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

        var columns = headerLine.Split(',');
        var columnNames = string.Join(", ", columns.Select(c => $"\"{c.Trim()}\""));

        await using var connection = await dataSource.OpenConnectionAsync();
        
        // Truncate table before import
        await using (var truncateCmd = new NpgsqlCommand($"TRUNCATE TABLE nald.\"{tableName}\"", connection))
        {
            await truncateCmd.ExecuteNonQueryAsync();
        }

        await using var writer = connection.BeginBinaryImport(
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
                // We keep the quotes for now to handle double-double quotes later, or we can handle them here.
                // Actually, let's handle them here for simplicity.
                // If we encounter a quote, and the next char is also a quote, it's an escaped quote.
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
                    await WriteRowAsync(writer, values, columns.Length, tableName);
                    values.Clear();
                    current.Clear();
                }
            }
            else
            {
                current.Append(c);
            }
        }

        if (values.Count > 0 || current.Length > 0)
        {
            values.Add(current.ToString());
            await WriteRowAsync(writer, values, columns.Length, tableName);
        }

        await writer.CompleteAsync();
    }

    private static async Task WriteRowAsync(NpgsqlBinaryImporter writer, List<string> values, int expectedColumnCount, string tableName)
    {
        if (values.Count != expectedColumnCount)
        {
            // Only log if it's not just an empty line
            if (values.Count > 1 || !string.IsNullOrWhiteSpace(values[0]))
            {
                Console.WriteLine($"Warning: Line in {tableName} has {values.Count} columns, expected {expectedColumnCount}. Skipping.");
            }
            
            return;
        }

        await writer.StartRowAsync();
        
        foreach (var value in values)
        {
            if (value == "null")
            {
                await writer.WriteNullAsync();
            }
            else
            {
                await writer.WriteAsync(value);
            }
        }
    }
}
