using Microsoft.Data.SqlClient;
using WALE.ProcessFile.Database.Interfaces;
using WALE.ProcessFile.Models;
using WALE.ProcessFile.Models.OutputSchema;

namespace WALE.ProcessFile.Database.Services;

public class SqlSeverAddServiceService(string connectionString) : IDatabaseAddService
{
    public async Task<ProcessRun> AddProcessRunAsync(ProcessRun processRun)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        const string sql = "INSERT INTO ProcessRun (Description, StartDateTimeUtc, NumberOfFiles) VALUES (@Description, @StartDateTimeUtc, @NumberOfFiles); SELECT SCOPE_IDENTITY()";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Description", processRun.Description);
        command.Parameters.AddWithValue("@StartDateTimeUtc", processRun.StartDateTimeUtc);
        command.Parameters.AddWithValue("@NumberOfFiles", processRun.NumberOfFiles);
        
        processRun.ProcessRunId = (int)(decimal)(await command.ExecuteScalarAsync())!;
        return processRun;
    }

    public async Task<int> SaveLicenceSetAsync(string licenceSetId, string shortLicenceSetId, int processRunId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        const string sql = "INSERT INTO LicenceSet (SchemaLicenceSetId, ShortLicenceSetId, ProcessRunId, DateTimeUtc) VALUES (@SchemaLicenceSetId, @ShortLicenceSetId, @ProcessRunId, @DateTimeUtc); SELECT SCOPE_IDENTITY()";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@SchemaLicenceSetId", licenceSetId);
        command.Parameters.AddWithValue("@ShortLicenceSetId", shortLicenceSetId);
        command.Parameters.AddWithValue("@ProcessRunId", processRunId);
        command.Parameters.AddWithValue("@DateTimeUtc", DateTime.UtcNow);
        
        return (int)(decimal)(await command.ExecuteScalarAsync())!;
    }

    public async Task<int> SaveLicenceAsync(string? licenceNumber, string licenceData, string? pdfFilePath, int processRunId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        
        const string sql = "INSERT INTO Licence (Filename, LicenceNumber, Data, ProcessRunId, DateTimeUtc) VALUES (@Filename, @LicenceNumber, @Data, @ProcessRunId, @DateTimeUtc); SELECT SCOPE_IDENTITY()";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Filename", pdfFilePath ?? "UNKNOWN");
        command.Parameters.AddWithValue("@LicenceNumber", licenceNumber ?? (object?)DBNull.Value);
        command.Parameters.AddWithValue("@Data", licenceData);
        command.Parameters.AddWithValue("@ProcessRunId", processRunId);
        command.Parameters.AddWithValue("@DateTimeUtc", DateTime.UtcNow);
        
        return (int)(decimal)(await command.ExecuteScalarAsync())!;
    }

    public async Task SaveMatchResultAsync(string matchesResult, string pdfFilePath, int processRunId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        const string sql = "INSERT INTO MatchesResult (Filename, Data, ProcessRunId, DateTimeUtc) VALUES (@Filename, @Data, @ProcessRunId, @DateTimeUtc)";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Filename", pdfFilePath);
        command.Parameters.AddWithValue("@Data", matchesResult);
        command.Parameters.AddWithValue("@ProcessRunId", processRunId);
        command.Parameters.AddWithValue("@DateTimeUtc", DateTime.UtcNow);
        
        await command.ExecuteNonQueryAsync();
    }

    public async Task SavePageScreenshotIfDoesntExistAsync(int pageNumber, string noOcrServiceName, string pdfFilename, byte[] data, int processRunId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        const string sql = "INSERT INTO PageScreenshot (Filename, PageNumber, NoOcrServiceName, Data, DateTimeUtc, ProcessRunId) VALUES (@Filename, @PageNumber, @NoOcrServiceName, @Data, @DateTimeUtc, @ProcessRunId)";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Filename", pdfFilename);
        command.Parameters.AddWithValue("@PageNumber", pageNumber);
        command.Parameters.AddWithValue("@NoOcrServiceName", noOcrServiceName);
        command.Parameters.AddWithValue("@Data", data);
        command.Parameters.AddWithValue("@DateTimeUtc", DateTime.UtcNow);
        command.Parameters.AddWithValue("@ProcessRunId", processRunId);
        
        await command.ExecuteNonQueryAsync();
    }

    public async Task<NoOcrServicePageCacheRequest> SaveNoOcrPageAsync(NoOcrServicePageCacheRequest request, string pageLines, int processRunId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        const string sql = "INSERT INTO NoOcrPageTextCache (Filename, PageNumber, NoOcrServiceName, Data, ProcessRunId, DateTimeUtc) VALUES (@Filename, @PageNumber, @NoOcrServiceName, @Data, @ProcessRunId, @DateTimeUtc)";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Filename", request.Filepath);
        command.Parameters.AddWithValue("@PageNumber", request.PageNumber);
        command.Parameters.AddWithValue("@NoOcrServiceName", request.NoOcrServiceName);
        command.Parameters.AddWithValue("@Data", pageLines);
        command.Parameters.AddWithValue("@ProcessRunId", processRunId);
        command.Parameters.AddWithValue("@DateTimeUtc", DateTime.UtcNow);
        
        await command.ExecuteNonQueryAsync();
        return request;
    }

    public async Task SaveNoOcrImagesMetadata(NoOcrServiceMetadataCacheRequest request, string imagesMetadataStr, int processRunId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        const string sql = "INSERT INTO NoOcrImagesMetadataCache (Filename, NoOcrServiceName, Response, DateTimeUtc, ProcessRunId) VALUES (@Filename, @NoOcrServiceName, @Response, @DateTimeUtc, @ProcessRunId)";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Filename", request.Filepath);
        command.Parameters.AddWithValue("@NoOcrServiceName", request.NoOcrServiceName);
        command.Parameters.AddWithValue("@Response", imagesMetadataStr);
        command.Parameters.AddWithValue("@DateTimeUtc", DateTime.UtcNow);
        command.Parameters.AddWithValue("@ProcessRunId", processRunId);
        
        await command.ExecuteNonQueryAsync();
    }

    public async Task<NoOcrServiceMetadataCacheRequest> SaveNoOcrPagesMetadata(NoOcrServiceMetadataCacheRequest request, string dataStr, int processRunId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        const string sql = "INSERT INTO NoOcrPagesMetadataCache (Filename, NoOcrServiceName, Response, DateTimeUtc, ProcessRunId) VALUES (@Filename, @NoOcrServiceName, @Response, @DateTimeUtc, @ProcessRunId)";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Filename", request.Filepath);
        command.Parameters.AddWithValue("@NoOcrServiceName", request.NoOcrServiceName);
        command.Parameters.AddWithValue("@Response", dataStr);
        command.Parameters.AddWithValue("@DateTimeUtc", DateTime.UtcNow);
        command.Parameters.AddWithValue("@ProcessRunId", processRunId);
        
        await command.ExecuteNonQueryAsync();
        return request;
    }

    public async Task SaveAllPagesTextIfDoesntExistAsync(string documentLinesStr, string pdfFilename, string noOcrServiceName, int processRunId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        const string sql = "INSERT INTO AllPagesText (Filename, NoOcrServiceName, Data, DateTimeUtc, ProcessRunId) VALUES (@Filename, @NoOcrServiceName, @Data, @DateTimeUtc, @ProcessRunId)";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Filename", pdfFilename);
        command.Parameters.AddWithValue("@NoOcrServiceName", noOcrServiceName);
        command.Parameters.AddWithValue("@Data", documentLinesStr);
        command.Parameters.AddWithValue("@DateTimeUtc", DateTime.UtcNow);
        command.Parameters.AddWithValue("@ProcessRunId", processRunId);
        
        await command.ExecuteNonQueryAsync();
    }

    public async Task SaveImageOnPageAsync(byte[] bytes, string pdfFilePath, string noOcrServiceName, int imageNumber, int pageNumber, string extension, int processRunId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        const string sql = "INSERT INTO ImageOnPage (Filename, NoOcrServiceName, ImageNumber, PageNumber, Data, Extension, DateTimeUtc, ProcessRunId) VALUES (@Filename, @NoOcrServiceName, @ImageNumber, @PageNumber, @Data, @Extension, @DateTimeUtc, @ProcessRunId)";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Filename", pdfFilePath);
        command.Parameters.AddWithValue("@NoOcrServiceName", noOcrServiceName);
        command.Parameters.AddWithValue("@Data", bytes);
        command.Parameters.AddWithValue("@ImageNumber", imageNumber);
        command.Parameters.AddWithValue("@PageNumber", pageNumber);        
        command.Parameters.AddWithValue("@Extension", extension);
        command.Parameters.AddWithValue("@DateTimeUtc", DateTime.UtcNow);
        command.Parameters.AddWithValue("@ProcessRunId", processRunId);
        
        await command.ExecuteNonQueryAsync();
    }

    public async Task SaveOcrImageTextAsync(OcrServiceImageTextCacheRequest request, string data, int processRunId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        const string sql = "INSERT INTO OcrImageTextCache (Filename, OcrServiceName, ImageNumber, PageNumber, Data, ProcessRunId, DateTimeUtc) VALUES (@Filename, @OcrServiceName, @ImageNumber, @PageNumber, @Data, @ProcessRunId, @DateTimeUtc)";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Filename", request.Filepath);
        command.Parameters.AddWithValue("@OcrServiceName", request.OcrServiceName);
        command.Parameters.AddWithValue("@Data", data);
        command.Parameters.AddWithValue("@ImageNumber", request.ImageNumber);
        command.Parameters.AddWithValue("@PageNumber", request.PageNumber);
        command.Parameters.AddWithValue("@ProcessRunId", processRunId);
        command.Parameters.AddWithValue("@DateTimeUtc", DateTime.UtcNow);
        
        await command.ExecuteNonQueryAsync();
    }
    
    public async Task ClearCacheAsync()
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        var sql =
            @"delete [dbo].[ImageOnPage]
            delete [dbo].[NoOcrImagesMetadataCache]
            delete [dbo].[NoOcrPagesMetadataCache]
            delete [dbo].[NoOcrPageTextCache]
            delete [dbo].[OcrImageTextCache]";

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

    public async Task UpdateProcessRunAsync(ProcessRun processRun)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        const string sql = "UPDATE ProcessRun SET EndDateTimeUtc = @EndDateTimeUtc WHERE ProcessRunId = @ProcessRunId";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ProcessRunId", processRun.ProcessRunId);
        command.Parameters.AddWithValue("@EndDateTimeUtc", DateTime.UtcNow);

        await command.ExecuteNonQueryAsync();
    }

    public async Task UpdateLicenceSetLicenceAsync(LicenceSetLicence licenceSetLicence)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        const string sql = "UPDATE LicenceSetLicence SET LicenceId = @LicenceId WHERE LicenceSetId = @LicenceSetId AND LicenceNumber = @LicenceNumber AND ProcessRunId = @ProcessRunId";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@LicenceSetId", licenceSetLicence.LicenceSetId);
        command.Parameters.AddWithValue("@LicenceId", licenceSetLicence.LicenceId);
        command.Parameters.AddWithValue("@LicenceNumber", licenceSetLicence.LicenceNumber);
        command.Parameters.AddWithValue("@ProcessRunId", licenceSetLicence.ProcessRunId);
        
        await command.ExecuteNonQueryAsync();
    }

    public async Task InsertLicenceSetLicenceAsync(
        int licenceSetId,
        int? licenceId,
        string? licenceNumber,
        string licenceVersionId,
        int processRunId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        const string sql = "INSERT INTO LicenceSetLicence (LicenceSetId, LicenceId, LicenceNumber, LicenceVersionId, ProcessRunId, DateTimeUtc) VALUES (@LicenceSetId, @LicenceId, @LicenceNumber, @LicenceVersionId, @ProcessRunId, @DateTimeUtc)";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@LicenceSetId", licenceSetId);
        command.Parameters.AddWithValue("@LicenceId", licenceId ?? (object?)DBNull.Value);
        command.Parameters.AddWithValue("@LicenceNumber", licenceNumber ?? "UNKNOWN");
        command.Parameters.AddWithValue("@LicenceVersionId", licenceVersionId);
        command.Parameters.AddWithValue("@ProcessRunId", processRunId);
        command.Parameters.AddWithValue("@DateTimeUtc", DateTime.UtcNow);
        
        await command.ExecuteNonQueryAsync();
    }

    public async Task SaveLicenceSetTypeAsync(int licenceSetId, int licenceSetType, int processRunId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        const string sql = "INSERT INTO LicenceSetType (LicenceSetId, LicenceSetType) VALUES (@LicenceSetId, @LicenceSetType)";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@LicenceSetId", licenceSetId);
        command.Parameters.AddWithValue("@LicenceSetType", licenceSetType);
        //command.Parameters.AddWithValue("@ProcessRunId", processRunId);
        //command.Parameters.AddWithValue("@DateTimeUtc", DateTime.UtcNow);
        
        await command.ExecuteNonQueryAsync();
    }

    public async Task SaveAggregateSetAsync(int licenceSetId, string? aggregateSetId, string data, int processRunId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        const string sql = "INSERT INTO AggregateSet (LicenceSetId, SchemaAggregateSetId, Data, ProcessRunId, DateTimeUtc) VALUES (@LicenceSetId, @SchemaAggregateSetId, @Data, @ProcessRunId, @DateTimeUtc)";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@LicenceSetId", licenceSetId);
        command.Parameters.AddWithValue("@SchemaAggregateSetId", aggregateSetId);
        command.Parameters.AddWithValue("@Data", data);
        command.Parameters.AddWithValue("@ProcessRunId", processRunId);
        command.Parameters.AddWithValue("@DateTimeUtc", DateTime.UtcNow);
        
        await command.ExecuteNonQueryAsync();
    }
}