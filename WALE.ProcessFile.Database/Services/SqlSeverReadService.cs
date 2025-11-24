using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.SqlClient;
using WALE.ProcessFile.Database.Interfaces;
using WALE.ProcessFile.Models;
using WALE.ProcessFile.Models.Enums.OutputSchema;
using WALE.ProcessFile.Models.OutputSchema;
using WALE.ProcessFile.Models.OutputSchema.Table;

namespace WALE.ProcessFile.Database.Services;

public class SqlSeverReadService(string connectionString) : IDatabaseReadService
{
    public async Task<string?> GetNoOcrPagesMetadataAsync(NoOcrServiceMetadataCacheRequest request)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        const string sql = "SELECT TOP 1 [Response] FROM NoOcrPagesMetadataCache WHERE Filename = @Filename AND NoOcrServiceName = @NoOcrServiceName";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Filename", request.Filepath);
        command.Parameters.AddWithValue("@NoOcrServiceName", request.NoOcrServiceName);
        
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            return reader.GetString(0);
        }

        return null;
    }
    
    public async Task<string?> GetNoOcrImagesMetadata(NoOcrServiceMetadataCacheRequest request)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        const string sql = "SELECT TOP 1 [Response] FROM NoOcrImagesMetadataCache WHERE Filename = @Filename AND NoOcrServiceName = @NoOcrServiceName";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Filename", request.Filepath);
        command.Parameters.AddWithValue("@NoOcrServiceName", request.NoOcrServiceName);
        
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            return reader.GetString(0);
        }

        return null;
    }

    public async Task<string?> GetOcrImageTextAsync(OcrServiceImageTextCacheRequest request)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        const string sql = "SELECT TOP 1 [Data] FROM OcrImageTextCache WHERE Filename = @Filename AND OcrServiceName = @OcrServiceName AND PageNumber = @PageNumber AND ImageNumber = @ImageNumber";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Filename", request.Filepath);
        command.Parameters.AddWithValue("@OcrServiceName", request.OcrServiceName);
        command.Parameters.AddWithValue("@PageNumber", request.PageNumber);
        command.Parameters.AddWithValue("@ImageNumber", request.ImageNumber);
        
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            return (string)reader.GetValue(0);
        }

        return null;
    }

    public async Task<string?> GetOcrScreenshotTextAsync(OcrServiceImageTextCacheRequest request)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        const string sql = "SELECT TOP 1 [Data] FROM OcrScreenshotTextCache WHERE Filename = @Filename AND OcrServiceName = @OcrServiceName AND PageNumber = @PageNumber";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Filename", request.Filepath);
        command.Parameters.AddWithValue("@OcrServiceName", request.OcrServiceName);
        command.Parameters.AddWithValue("@PageNumber", request.PageNumber);
        
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            return (string)reader.GetValue(0);
        }

        return null;
    }

    public async Task<byte[]?> GetImageBytesAsync(OcrServiceImageDataCacheRequest request)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        const string sql = "SELECT TOP 1 [Data] FROM ImageOnPage WHERE Filename = @Filename AND NoOcrServiceName = @NoOcrServiceName AND PageNumber = @PageNumber AND ImageNumber = @ImageNumber AND Extension = @Extension";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Filename", request.Filepath);
        command.Parameters.AddWithValue("@NoOcrServiceName", request.NoOcrServiceName);
        command.Parameters.AddWithValue("@PageNumber", request.PageNumber);
        command.Parameters.AddWithValue("@ImageNumber", request.ImageNumber);
        command.Parameters.AddWithValue("@Extension", request.Extension);
        
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            return (byte [])reader.GetValue(0);
        }

        return null;
    }

    public async Task<List<(int imageNumber, string extension)>> GetImagesAsync(OcrServiceImageDataCacheRequest request)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        const string sql = "SELECT ImageNumber, Extension FROM ImageOnPage WHERE Filename = @Filename AND PageNumber = @PageNumber";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Filename", request.Filepath);
        command.Parameters.AddWithValue("@PageNumber", request.PageNumber);
        
        await using var reader = await command.ExecuteReaderAsync();
        var returnList = new List<(int imageNumber, string extension)>();
        
        while (await reader.ReadAsync())
        {
            returnList.Add((
                reader.GetInt32(0),
                reader.GetString(1)));
        }

        return returnList;
    }

    public async Task<byte[]?> GetPageScreenshotAsync(int pageNumber, string fileName, string noOcrServiceName)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        const string sql = "SELECT TOP 1 [Data] FROM PageScreenshot WHERE Filename = @Filename AND NoOcrServiceName = @NoOcrServiceName AND PageNumber = @PageNumber";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Filename", fileName);
        command.Parameters.AddWithValue("@NoOcrServiceName", noOcrServiceName);
        command.Parameters.AddWithValue("@PageNumber", pageNumber);
        
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            return (byte [])reader.GetValue(0);
        }

        return null;
    }

    public async Task<string?> GetNoOcrPageTextLinesAsync(NoOcrServicePageCacheRequest request)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        const string sql = "SELECT TOP 1 [Data] FROM NoOcrPageTextCache WHERE Filename = @Filename AND PageNumber = @PageNumber AND NoOcrServiceName = @NoOcrServiceName";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Filename", request.Filepath);
        command.Parameters.AddWithValue("@PageNumber", request.PageNumber);
        command.Parameters.AddWithValue("@NoOcrServiceName", request.NoOcrServiceName);
        
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            return reader.GetString(0);
        }

        return null;
    }
    
    public async Task<string?> GetAllPagesTextAsync(string pdfFilename, string noOcrServiceName)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        const string sql = "SELECT TOP 1 [Data] FROM AllPagesText WHERE Filename = @Filename AND NoOcrServiceName = @NoOcrServiceName";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Filename", pdfFilename);
        command.Parameters.AddWithValue("@NoOcrServiceName", noOcrServiceName);
        
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            return reader.GetString(0);
        }

        return null;
    }
    
    public async Task<List<ProcessRun>> GetProcessRunsAsync()
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        const string sql = "SELECT ProcessRunId, Description, StartDateTimeUtc, EndDateTimeUtc, NumberOfFiles FROM ProcessRun";
        await using var command = new SqlCommand(sql, connection);
        
        await using var reader = await command.ExecuteReaderAsync();
        var returnList = new List<ProcessRun>();
        
        while (await reader.ReadAsync())
        {
            returnList.Add(new ProcessRun
            {
                ProcessRunId = reader.GetInt32(0),
                Description = reader.GetString(1),
                StartDateTimeUtc = reader.GetDateTime(2),
                EndDateTimeUtc = reader.IsDBNull(3) ? null : reader.GetDateTime(3),
                NumberOfFiles = reader.GetInt32(4)
            });
        }

        return returnList;
    }

    public async Task<ProcessRun?> GetMostRecentProcessRunAsync(string filename)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        const string sql = "SELECT TOP 1 Licence.ProcessRunId, ProcessRun.Description, ProcessRun.StartDateTimeUtc, ProcessRun.EndDateTimeUtc, ProcessRun.NumberOfFiles FROM Licence JOIN ProcessRun ON Licence.ProcessRunId = ProcessRun.ProcessRunId WHERE Filename = @Filename ORDER BY Licence.ProcessRunId DESC";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Filename", filename);
        
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            return new ProcessRun
            {
                ProcessRunId = reader.GetInt32(0),
                Description = reader.GetString(1),
                StartDateTimeUtc = reader.GetDateTime(2),
                EndDateTimeUtc =  reader.IsDBNull(3) ? null : reader.GetDateTime(3),
                NumberOfFiles = reader.GetInt32(4)
            };
        }

        return null;
    }

    public async Task<List<Licence>> GetLicencesAsync(int processRunId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        const string sql = "SELECT Data, LicenceId FROM Licence WHERE ProcessRunId = @ProcessRunId";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ProcessRunId", processRunId);
        
        await using var reader = await command.ExecuteReaderAsync();
        var returnList = new List<Licence>();
        
        while (await reader.ReadAsync())
        {
            var dataStr = reader.GetString(0);
            var licenceId = reader.GetInt32(1);
            
            var data = JsonSerializer.Deserialize<Licence>(dataStr, GetSerializerOptions())!;
            data.NoneSchemaData.TryAdd("licenceId", licenceId);
            
            returnList.Add(data);
        }

        return returnList;
    }

    public async Task<List<LicenceSetTable>> GetLicenceSetsSimpleAsync(int processRunId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        const string sql = "SELECT LicenceSetId, ShortLicenceSetId, SchemaLicenceSetId FROM LicenceSet WHERE ProcessRunId = @ProcessRunId";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ProcessRunId", processRunId);
        
        await using var reader = await command.ExecuteReaderAsync();
        var returnList = new List<LicenceSetTable>();
        
        while (await reader.ReadAsync())
        {
            var licenceSetId = reader.GetInt32(0);
            var shortLicenceSetId = reader.GetString(1);
            var schemaLicenceSetId = reader.GetString(2);
            
            returnList.Add(new LicenceSetTable
            {
                LicenceSetId = licenceSetId,
                ShortLicenceSetId = shortLicenceSetId,
                SchemaLicenceSetId = schemaLicenceSetId
            });
        }

        return returnList;
    }

    public async Task<List<LicenceSetTable>> GetLicenceSetsSimpleAsync(string filename, int processRunId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        const string sql = "SELECT LicenceSetId, ShortLicenceSetId, SchemaLicenceSetId FROM LicenceSet WHERE ProcessRunId = @ProcessRunId AND Filename = @Filename";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ProcessRunId", processRunId);
        command.Parameters.AddWithValue("@Filename", filename);
        
        await using var reader = await command.ExecuteReaderAsync();
        var returnList = new List<LicenceSetTable>();
        
        while (await reader.ReadAsync())
        {
            var licenceSetId = reader.GetInt32(0);
            var shortLicenceSetId = reader.GetString(1);
            var schemaLicenceSetId = reader.GetString(2);
            
            returnList.Add(new LicenceSetTable
            {
                LicenceSetId = licenceSetId,
                ShortLicenceSetId = shortLicenceSetId,
                SchemaLicenceSetId = schemaLicenceSetId
            });
        }

        return returnList;
    }

    public async Task<List<LicenceSetLicence>> GetLicenceSetLicencesAsync(int processRunId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        const string sql = "SELECT LicenceId, LicenceNumber, LicenceVersionId, LicenceSetId FROM LicenceSetLicence WHERE ProcessRunId = @ProcessRunId";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ProcessRunId", processRunId);
        
        await using var reader = await command.ExecuteReaderAsync();
        var returnList = new List<LicenceSetLicence>();
        
        while (await reader.ReadAsync())
        {
            var licenceId = reader.IsDBNull(0) ? (int?)null : reader.GetInt32(0);
            var licenceNumber = reader.GetString(1);
            var licenceVersionId = reader.GetString(2);
            var licenceSetId = reader.GetInt32(3);
            
            returnList.Add(new LicenceSetLicence
            {
                LicenceId = licenceId,
                LicenceNumber = licenceNumber,
                LicenceVersionId = licenceVersionId,
                LicenceSetId = licenceSetId,
                ProcessRunId = processRunId
            });
        }

        return returnList;
    }

    public async Task<List<LicenceSetLicence>> GetLicenceSetLicencesAsync(int licenceSetId, int processRunId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        const string sql = "SELECT LicenceId, LicenceNumber, LicenceVersionId FROM LicenceSetLicence WHERE LicenceSetId = @LicenceSetId AND ProcessRunId = @ProcessRunId";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@LicenceSetId", licenceSetId);
        command.Parameters.AddWithValue("@ProcessRunId", processRunId);
        
        await using var reader = await command.ExecuteReaderAsync();
        var returnList = new List<LicenceSetLicence>();
        
        while (await reader.ReadAsync())
        {
            var licenceId = reader.IsDBNull(0) ? (int?)null : reader.GetInt32(0);
            var licenceNumber = reader.GetString(1);
            var licenceVersionId = reader.GetString(2);
            
            returnList.Add(new LicenceSetLicence
            {
                LicenceId = licenceId,
                LicenceNumber = licenceNumber,
                LicenceVersionId = licenceVersionId,
                LicenceSetId = licenceSetId,
                ProcessRunId = processRunId
            });
        }

        return returnList;
    }

    public async Task<LicenceSetType[]> GetLicenceSetTypes(int licenceSetId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        const string sql = "SELECT LicenceSetType FROM LicenceSetType WHERE LicenceSetId = @LicenceSetId";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@LicenceSetId", licenceSetId);
        
        await using var reader = await command.ExecuteReaderAsync();
        var returnList = new List<LicenceSetType>();
        
        while (await reader.ReadAsync())
        {
            var licenceSetType = reader.GetInt32(0);
            
            returnList.Add((LicenceSetType)licenceSetType);
        }

        return returnList.ToArray();
    }

    public async Task<List<(int LicenceSetId, LicenceSetType Type)>> GetLicenceSetTypesForProcessRun(int processRunId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        const string sql = @"SELECT
                lst.LicenceSetId
                , lst.LicenceSetType
            FROM LicenceSetType lst
            JOIN LicenceSet ls on ls.LicenceSetId = lst.LicenceSetId
            WHERE
                ls.ProcessRunId = @ProcessRunId";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ProcessRunId", processRunId);
        
        await using var reader = await command.ExecuteReaderAsync();
        var returnList = new List<(int, LicenceSetType)>();
        
        while (await reader.ReadAsync())
        {
            var licenceSetId = reader.GetInt32(0);
            var licenceSetType = reader.GetInt32(1);
            
            returnList.Add((licenceSetId, (LicenceSetType)licenceSetType));
        }

        return returnList;
    }

    public async Task<AggregateSet[]?> GetAggregateSets(int licenceSetId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        const string sql = "SELECT AggregateSetId, SchemaAggregateSetId, Data FROM AggregateSet WHERE LicenceSetId = @LicenceSetId";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@LicenceSetId", licenceSetId);
        
        await using var reader = await command.ExecuteReaderAsync();
        var returnList = new List<AggregateSet>();
        
        while (await reader.ReadAsync())
        {
            returnList.Add(new AggregateSet()); // TOOD add the detail
        }

        return returnList.ToArray();
    }

    public async Task<List<(int LicenceSetId, AggregateSet AggregateSet)>> GetAggregateSetsForProcessRun(int processRunId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        const string sql = "SELECT AggregateSetId, SchemaAggregateSetId, Data FROM AggregateSet WHERE ProcessRunId = @ProcessRunId";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ProcessRunId", processRunId);
        
        await using var reader = await command.ExecuteReaderAsync();
        var returnList = new List<(int LicenceSetId, AggregateSet AggregateSet)>();
        
        while (await reader.ReadAsync())
        {
            returnList.Add((-1, new AggregateSet())); // TOOD add the detail
        }

        return returnList;
    }

    public async Task<Licence?> GetLicenceAsync(string licenceNumber, int processRunId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        const string sql = "SELECT TOP 1 Data, LicenceId FROM Licence WHERE LicenceNumber = @LicenceNumber AND ProcessRunId = @ProcessRunId";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@LicenceNumber", licenceNumber);
        command.Parameters.AddWithValue("@ProcessRunId", processRunId);
        
        await using var reader = await command.ExecuteReaderAsync();
        
        while (await reader.ReadAsync())
        {
            var dataStr = reader.GetString(0);
            var licenceId = reader.GetInt32(1);
            
            var data = JsonSerializer.Deserialize<Licence>(dataStr, GetSerializerOptions())!;
            data.NoneSchemaData.TryAdd("licenceId", licenceId);
            
            return data;
        }

        return null;
    }

    public async Task<Licence?> GetLicenceAsync(string filename)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        const string sql = "SELECT TOP 1 Data, LicenceId FROM Licence WHERE Filename = @Filename ORDER BY ProcessRunId DESC";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Filename", filename);
        
        await using var reader = await command.ExecuteReaderAsync();
        
        while (await reader.ReadAsync())
        {
            var dataStr = reader.GetString(0);
            var licenceId = reader.GetInt32(1);
            
            var data = JsonSerializer.Deserialize<Licence>(dataStr, GetSerializerOptions())!;
            data.NoneSchemaData.TryAdd("licenceId", licenceId);
            
            return data;
        }

        return null;
    }

    public async Task<MatchesResult?> GetMatchesResult(string filename)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        const string sql = "SELECT TOP 1 Data FROM MatchesResult WHERE Filename = @Filename ORDER BY ProcessRunId DESC";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Filename", filename);
        
        await using var reader = await command.ExecuteReaderAsync();
        
        while (await reader.ReadAsync())
        {
            var dataStr = reader.GetString(0);
            var data = JsonSerializer.Deserialize<MatchesResult>(dataStr, GetSerializerOptions())!;
            
            return data;
        }

        return null;
    }

    // TODO move to a 'Core' layer
    private static JsonSerializerOptions GetSerializerOptions()
    {
        return new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters =
            {
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
            }
        };
    }
}