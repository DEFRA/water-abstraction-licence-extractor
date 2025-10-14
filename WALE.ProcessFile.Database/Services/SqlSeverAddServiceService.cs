using Microsoft.Data.SqlClient;
using WALE.ProcessFile.Database.Interfaces;
using WALE.ProcessFile.Models;

namespace WALE.ProcessFile.Database.Services;

public class SqlSeverAddServiceService(string connectionString) : IDatabaseAddService
{
    public async Task<ProcessRun> AddProcessRunAsync(ProcessRun processRun)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        const string sql = "INSERT INTO ProcessRun (Description, StartDateTimeUtc, NumberOfFiles) VALUES (@Description, @StartDateTimeUtc, @NumberOfFiles)";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Description", processRun.Description);
        command.Parameters.AddWithValue("@StartDateTimeUtc", processRun.StartDateTimeUtc);
        command.Parameters.AddWithValue("@NumberOfFiles", processRun.NumberOfFiles);
        
        await command.ExecuteNonQueryAsync();
        return processRun;
    }

    public async Task SaveLicenceSetAsync(string licenceSet, string licenceSetId, string shortLicenceSetId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        const string sql = "INSERT INTO LicenceSet (SchemaLicenceSetId, ShortLicenceSetId, Data) VALUES (@SchemaLicenceSetId, @ShortLicenceSetId, @Data)";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@SchemaLicenceSetId", licenceSetId);
        command.Parameters.AddWithValue("@ShortLicenceSetId", shortLicenceSetId);
        command.Parameters.AddWithValue("@Data", licenceSet);
        
        await command.ExecuteNonQueryAsync();
    }

    public async Task SaveLicenceAsync(string licence, string pdfFilePath)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        const string sql = "INSERT INTO Licence (Filename, Data) VALUES (@Filename, @Data)";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Filename", pdfFilePath);
        command.Parameters.AddWithValue("@Data", licence);
        
        await command.ExecuteNonQueryAsync();
    }

    public async Task SaveMatchResultAsync(string matchesResult, string pdfFilePath)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        const string sql = "INSERT INTO MatchesResult (Filename, Data) VALUES (@Filename, @Data)";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Filename", pdfFilePath);
        command.Parameters.AddWithValue("@Data", matchesResult);
        
        await command.ExecuteNonQueryAsync();
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

    public async Task SaveOcrImageTextAsync(OcrServiceImageTextCacheRequest request, string data)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        const string sql = "INSERT INTO OcrImageTextCache (Filename, OcrServiceName, ImageNumber, PageNumber, Data) VALUES (@Filename, @OcrServiceName, @ImageNumber, @PageNumber, @Data)";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Filename", request.Filepath);
        command.Parameters.AddWithValue("@OcrServiceName", request.OcrServiceName);
        command.Parameters.AddWithValue("@Data", data);
        command.Parameters.AddWithValue("@ImageNumber", request.ImageNumber);
        command.Parameters.AddWithValue("@PageNumber", request.PageNumber); 
        
        await command.ExecuteNonQueryAsync();
    }
    
    public async Task ClearCacheAsync()
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        var sql =
            @"delete [dbo].[AllPagesText]
            delete [dbo].[ImageOnPage]
            delete [dbo].[NoOcrImagesMetadataCache]
            delete [dbo].[NoOcrPagesMetadataCache]
            delete [dbo].[NoOcrPageTextCache]
            delete [dbo].[OcrImageTextCache]
            delete [dbo].[PageScreenshot]";

        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
    
    public async Task ClearCacheAsync(string pdfFilename)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        var sql =
            @"delete [dbo].[AllPagesText] WHERE Filename = @Filename
            delete [dbo].[ImageOnPage] WHERE Filename = @Filename
            delete [dbo].[NoOcrImagesMetadataCache] WHERE Filename = @Filename
            delete [dbo].[NoOcrPagesMetadataCache] WHERE Filename = @Filename
            delete [dbo].[NoOcrPageTextCache] WHERE Filename = @Filename
            delete [dbo].[OcrImageTextCache] WHERE Filename = @Filename
            delete [dbo].[PageScreenshot] WHERE Filename = @Filename";

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Filename", pdfFilename);
        
        await command.ExecuteNonQueryAsync();
    }
}