using Microsoft.Data.SqlClient;
using WALE.ProcessFile.Database.Interfaces;
using WALE.ProcessFile.Models;
using WALE.ProcessFile.Models.OutputSchema;

namespace WALE.ProcessFile.Database.Services;

public class SqlSeverAddServiceService(string connectionString) : IDatabaseAddService
{
    public Task<ProcessRun> AddProcessRunAsync(ProcessRun processRun)
    {
        throw new NotImplementedException();
    }

    public Task SaveLicenceSetsAsync(IReadOnlyList<LicenceSet> licenceSets, string pdfFilePath)
    {
        throw new NotImplementedException();
    }

    public Task SaveLicenceAsync(Licence licence, string pdfFilePath)
    {
        throw new NotImplementedException();
    }

    public Task SaveMatchResultAsync(MatchesResult matchesResult, string pdfFilePath)
    {
        throw new NotImplementedException();
    }

    public Task SaveListDataAsync(List<OutputListDataItem> listData)
    {
        throw new NotImplementedException();
    }

    public async Task SavePageScreenshotIfDoesntExistAsync(int pageNumber, string noOcrServiceName, string pdfFilename, byte[] data)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        const string sql = "INSERT INTO PageScreenshot (Filename, PageNumber, NoOcrServiceName, Data, DateTimeUtc) VALUES (@Filename, @PageNumber, @NoOcrServiceName, @Data, @DateTimeUtc)";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Filename", pdfFilename);
        command.Parameters.AddWithValue("@PageNumber", pageNumber);
        command.Parameters.AddWithValue("@NoOcrServiceName", noOcrServiceName);
        command.Parameters.AddWithValue("@Data", data);
        command.Parameters.AddWithValue("@DateTimeUtc", DateTime.UtcNow);
        
        await command.ExecuteNonQueryAsync();
    }

    public async Task<NoOcrServicePageCacheRequest> SaveNoOcrPageAsync(NoOcrServicePageCacheRequest request, string pageLines)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        const string sql = "INSERT INTO NoOcrPageTextCache (Filename, PageNumber, NoOcrServiceName, Data) VALUES (@Filename, @PageNumber, @NoOcrServiceName, @Data)";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Filename", request.Filepath);
        command.Parameters.AddWithValue("@PageNumber", request.PageNumber);
        command.Parameters.AddWithValue("@NoOcrServiceName", request.NoOcrServiceName);
        command.Parameters.AddWithValue("@Data", pageLines);
        
        await command.ExecuteNonQueryAsync();
        return request;
    }

    public async Task SaveNoOcrImagesMetadata(NoOcrServiceMetadataCacheRequest request, string imagesMetadataStr)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        const string sql = "INSERT INTO NoOcrImagesMetadataCache (Filename, NoOcrServiceName, Response, DateTimeUtc) VALUES (@Filename, @NoOcrServiceName, @Response, @DateTimeUtc)";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Filename", request.Filepath);
        command.Parameters.AddWithValue("@NoOcrServiceName", request.NoOcrServiceName);
        command.Parameters.AddWithValue("@Response", imagesMetadataStr);
        command.Parameters.AddWithValue("@DateTimeUtc", DateTime.UtcNow);
        
        await command.ExecuteNonQueryAsync();
    }

    public async Task<NoOcrServiceMetadataCacheRequest> SaveNoOcrPagesMetadata(NoOcrServiceMetadataCacheRequest request, string dataStr)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        const string sql = "INSERT INTO NoOcrPagesMetadataCache (Filename, NoOcrServiceName, Response, DateTimeUtc) VALUES (@Filename, @NoOcrServiceName, @Response, @DateTimeUtc)";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Filename", request.Filepath);
        command.Parameters.AddWithValue("@NoOcrServiceName", request.NoOcrServiceName);
        command.Parameters.AddWithValue("@Response", dataStr);
        command.Parameters.AddWithValue("@DateTimeUtc", DateTime.UtcNow);
        
        await command.ExecuteNonQueryAsync();
        return request;
    }

    public async Task SaveAllPagesTextIfDoesntExistAsync(string documentLinesStr, string pdfFilename, string noOcrServiceName)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        const string sql = "INSERT INTO AllPagesText (Filename, NoOcrServiceName, Data, DateTimeUtc) VALUES (@Filename, @NoOcrServiceName, @Data, @DateTimeUtc)";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Filename", pdfFilename);
        command.Parameters.AddWithValue("@NoOcrServiceName", noOcrServiceName);
        command.Parameters.AddWithValue("@Data", documentLinesStr);
        command.Parameters.AddWithValue("@DateTimeUtc", DateTime.UtcNow);
        
        await command.ExecuteNonQueryAsync();
    }

    public async Task SaveImageOnPageAsync(byte[] bytes, string pdfFilePath, string noOcrServiceName, int imageNumber, int pageNumber, string extension)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        const string sql = "INSERT INTO ImageOnPage (Filename, NoOcrServiceName, ImageNumber, PageNumber, Data, Extension, DateTimeUtc) VALUES (@Filename, @NoOcrServiceName, @ImageNumber, @PageNumber, @Data, @Extension, @DateTimeUtc)";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Filename", pdfFilePath);
        command.Parameters.AddWithValue("@NoOcrServiceName", noOcrServiceName);
        command.Parameters.AddWithValue("@Data", bytes);
        command.Parameters.AddWithValue("@ImageNumber", imageNumber);
        command.Parameters.AddWithValue("@PageNumber", pageNumber);        
        command.Parameters.AddWithValue("@Extension", extension);
        command.Parameters.AddWithValue("@DateTimeUtc", DateTime.UtcNow);
        
        await command.ExecuteNonQueryAsync();
    }
}