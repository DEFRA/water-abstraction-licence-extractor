using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.SqlClient;
using WALE.ProcessFile.Database.Interfaces;
using WALE.ProcessFile.Models;
using WALE.ProcessFile.Models.Enums.OutputSchema;
using WALE.ProcessFile.Models.OutputSchema;

namespace WALE.ProcessFile.Database.Services;

public class SqlSeverReadServiceService(string connectionString) : IDatabaseReadService
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

    public async Task<byte[]?> GetImageBytesAsync(OcrServiceImageDataCacheRequest request)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        const string sql = "SELECT TOP 1 [Data] FROM ImageOnPage WHERE Filename = @Filename AND NoOcrServiceName = @NoOcrServiceName AND PageNumber = @PageNumber";
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

    public async Task<List<Licence>> GetLicencesAsync(int processRunId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        const string sql = "SELECT Data FROM Licence WHERE ProcessRunId = @ProcessRunId";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ProcessRunId", processRunId);
        
        await using var reader = await command.ExecuteReaderAsync();
        var returnList = new List<Licence>();
        
        while (await reader.ReadAsync())
        {
            var dataStr = reader.GetString(0);
            var data = JsonSerializer.Deserialize<Licence>(dataStr, GetSerializerOptions())!;
            
            returnList.Add(data);
        }

        return returnList;
    }

    public async Task<List<int>> GetLicenceSetIdsAsync(int processRunId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        const string sql = "SELECT LicenceSetId, ProcessRunId FROM LicenceSet WHERE ProcessRunId = @ProcessRunId";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ProcessRunId", processRunId);
        
        await using var reader = await command.ExecuteReaderAsync();
        var returnList = new List<int>();
        
        while (await reader.ReadAsync())
        {
            var licenceSetId = reader.GetInt32(0);
            returnList.Add(licenceSetId);
        }

        return returnList;
    }

    public async Task<List<LicenceSetLicence>> GetLicenceSetLicencesAsync(int licenceSetId, int processRunId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        const string sql = "SELECT LicenceNumber, LicenceVersionId FROM LicenceSetLicence WHERE LicenceSetId = @LicenceSetId AND ProcessRunId = @ProcessRunId";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@LicenceSetId", licenceSetId);
        command.Parameters.AddWithValue("@ProcessRunId", processRunId);
        
        await using var reader = await command.ExecuteReaderAsync();
        var returnList = new List<LicenceSetLicence>();
        
        while (await reader.ReadAsync())
        {
            var licenceNumber = reader.GetString(0);
            var licenceVersionId = reader.GetString(1);
            
            returnList.Add(new LicenceSetLicence
            {
                LicenceNumber = licenceNumber,
                LicenceVersionId = licenceVersionId
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