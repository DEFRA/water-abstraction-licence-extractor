using Microsoft.Data.SqlClient;
using WALE.ProcessFile.Database.Interfaces;
using WALE.ProcessFile.Models;

namespace WALE.ProcessFile.Database.Services;

public class SqlSeverReadServiceService(string connectionString) : IDatabaseReadService
{
    public List<ProcessRun> GetProcessRuns()
    {
        throw new NotImplementedException();
    }

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
}