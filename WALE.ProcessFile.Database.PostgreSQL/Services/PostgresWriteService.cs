using Dapper;
using Npgsql;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OutputSchema;

namespace WALE.ProcessFile.Database.PostgreSQL.Services;

public class PostgresWriteService(PostgresDataSourceProvider dataSourceProvider)
    : IDatabaseWriteService
{
    public async Task<ProcessRun> AddProcessRunAsync(ProcessRun processRun)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           INSERT INTO ProcessRun (Description, StartDateTimeUtc, NumberOfFiles) 
                           VALUES (@Description, @StartDateTimeUtc, @NumberOfFiles) 
                           RETURNING ProcessRunId
                           """;

        processRun.ProcessRunId = await connection.ExecuteScalarAsync<int>(sql, new
        {
            processRun.Description,
            processRun.StartDateTimeUtc,
            processRun.NumberOfFiles
        });

        return processRun;
    }

    public async Task<int> SaveLicenceSetAsync(string licenceSetId, string shortLicenceSetId, int processRunId)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           INSERT INTO LicenceSet (SchemaLicenceSetId, ShortLicenceSetId, ProcessRunId, DateTimeUtc) 
                           VALUES (@SchemaLicenceSetId, @ShortLicenceSetId, @ProcessRunId, @DateTimeUtc)
                           RETURNING LicenceSetId
                           """;

        return await connection.ExecuteScalarAsync<int>(sql, new
        {
            SchemaLicenceSetId = licenceSetId,
            ShortLicenceSetId = shortLicenceSetId,
            ProcessRunId = processRunId,
            DateTimeUtc = DateTime.UtcNow
        });
    }

    public async Task<int> SaveLicenceAsync(string? licenceNumber, string licenceData, string? pdfFilePath,
        int processRunId)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           INSERT INTO Licence (Filename, LicenceNumber, Data, ProcessRunId, DateTimeUtc)
                           VALUES (@Filename, @LicenceNumber, @Data, @ProcessRunId, @DateTimeUtc)
                           RETURNING LicenceId
                           """;

        return await connection.ExecuteScalarAsync<int>(sql, new
        {
            Filename = pdfFilePath ?? "UNKNOWN",
            LicenceNumber = licenceNumber,
            Data = licenceData,
            ProcessRunId = processRunId,
            DateTimeUtc = DateTime.UtcNow
        });
    }

    public async Task SaveMatchAsync(int matchesResultId, string? labelName, string? labelGroupName, string data)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           INSERT INTO Match (MatchesResultId, LabelName, LabelGroupName, Data)
                           VALUES (@MatchesResultId, @LabelName, @LabelGroupName, @Data)
                           """;

        await connection.ExecuteAsync(sql, new
        {
            MatchesResultId = matchesResultId,
            LabelName = labelName,
            LabelGroupName = labelGroupName,
            Data = data
        });
    }

    public async Task<int> SaveMatchesResultAsync(string matchesResult, string pdfFilePath, int processRunId)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           INSERT INTO MatchesResult (Filename, Data, ProcessRunId, DateTimeUtc)
                           VALUES (@Filename, @Data, @ProcessRunId, @DateTimeUtc)
                           RETURNING MatchesResultId
                           """;

        return await connection.ExecuteScalarAsync<int>(sql, new
        {
            Filename = pdfFilePath,
            Data = matchesResult,
            ProcessRunId = processRunId,
            DateTimeUtc = DateTime.UtcNow
        });
    }

    public async Task SavePageScreenshotIfDoesntExistAsync(int pageNumber, string noOcrServiceName, string pdfFilename,
        byte[] data, int processRunId)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           INSERT INTO PageScreenshot (Filename, PageNumber, NoOcrServiceName, Data, DateTimeUtc, ProcessRunId)
                           VALUES (@Filename, @PageNumber, @NoOcrServiceName, @Data, @DateTimeUtc, @ProcessRunId)
                           """;
        
        await connection.ExecuteAsync(sql, new
        {
            Filename = pdfFilename,
            PageNumber = pageNumber,
            NoOcrServiceName = noOcrServiceName,
            Data = data,
            DateTimeUtc = DateTime.UtcNow,
            ProcessRunId = processRunId
        });
    }

    public async Task<NoOcrServicePageCacheRequest> SaveNoOcrPageAsync(NoOcrServicePageCacheRequest request,
        string pageLines, int processRunId)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           INSERT INTO NoOcrPageTextCache (Filename, PageNumber, NoOcrServiceName, Data, ProcessRunId, DateTimeUtc)
                           VALUES (@Filename, @PageNumber, @NoOcrServiceName, @Data, @ProcessRunId, @DateTimeUtc)
                           """;

        await connection.ExecuteAsync(sql, new
        {
            Filename = request.Filepath,
            request.PageNumber,
            request.NoOcrServiceName,
            Data = pageLines,
            ProcessRunId = processRunId,
            DateTimeUtc = DateTime.UtcNow
        });
        
        return request;
    }

    public async Task SaveNoOcrImagesMetadata(NoOcrServiceMetadataCacheRequest request, string imagesMetadataStr,
        int processRunId)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           INSERT INTO NoOcrImagesMetadataCache (Filename, NoOcrServiceName, Response, DateTimeUtc, ProcessRunId)
                           VALUES (@Filename, @NoOcrServiceName, @Response, @DateTimeUtc, @ProcessRunId)
                           """;
        
        await connection.ExecuteAsync(sql, new
        {
            Filename = request.Filepath,
            request.NoOcrServiceName,
            Response = imagesMetadataStr,
            DateTimeUtc = DateTime.UtcNow,
            ProcessRunId = processRunId
        });
    }

    public async Task<NoOcrServiceMetadataCacheRequest> SaveNoOcrPagesMetadata(NoOcrServiceMetadataCacheRequest request,
        string dataStr, int processRunId)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           INSERT INTO NoOcrPagesMetadataCache (Filename, NoOcrServiceName, Response, DateTimeUtc, ProcessRunId) 
                           VALUES (@Filename, @NoOcrServiceName, @Response, @DateTimeUtc, @ProcessRunId)
                           """;
        await connection.ExecuteAsync(sql, new
        {
            Filename = request.Filepath,
            request.NoOcrServiceName,
            Response = dataStr,
            DateTimeUtc = DateTime.UtcNow,
            ProcessRunId = processRunId
        });
        
        return request;
    }

    public async Task SaveAllPagesTextIfDoesntExistAsync(string documentLinesStr, string pdfFilename,
        string noOcrServiceName,
        int processRunId)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           INSERT INTO AllPagesText (Filename, NoOcrServiceName, Data, DateTimeUtc, ProcessRunId)
                           VALUES (@Filename, @NoOcrServiceName, @Data, @DateTimeUtc, @ProcessRunId)
                           """;

        await connection.ExecuteAsync(sql, new
        {
            Filename = pdfFilename,
            NoOcrServiceName = noOcrServiceName,
            Data = documentLinesStr,
            DateTimeUtc = DateTime.UtcNow,
            ProcessRunId = processRunId
        });
    }

    public async Task SaveImageOnPageAsync(byte[] bytes, string pdfFilePath, string noOcrServiceName, int imageNumber,
        int pageNumber,
        string extension, int processRunId)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           INSERT INTO ImageOnPage (Filename, NoOcrServiceName, ImageNumber, PageNumber, Data, Extension, DateTimeUtc, ProcessRunId) 
                           VALUES (@Filename, @NoOcrServiceName, @ImageNumber, @PageNumber, @Data, @Extension, @DateTimeUtc, @ProcessRunId)
                           """;

        await connection.ExecuteAsync(sql, new
        {
            Filename = pdfFilePath,
            NoOcrServiceName = noOcrServiceName,
            Data = bytes,
            ImageNumber = imageNumber,
            PageNumber = pageNumber,
            Extension = extension,
            DateTimeUtc = DateTime.UtcNow,
            ProcessRunId = processRunId
        });
    }

    public async Task SaveOcrImageTextAsync(OcrServiceImageTextCacheRequest request, string data, int processRunId)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           INSERT INTO OcrImageTextCache (Filename, OcrServiceName, ImageNumber, PageNumber, Data, ProcessRunId, DateTimeUtc)
                           VALUES (@Filename, @OcrServiceName, @ImageNumber, @PageNumber, @Data, @ProcessRunId, @DateTimeUtc)
                           """;

        await connection.ExecuteAsync(sql, new
        {
            Filename = request.Filepath,
            request.OcrServiceName,
            Data = data,
            request.ImageNumber,
            request.PageNumber,
            ProcessRunId = processRunId,
            DateTimeUtc = DateTime.UtcNow
        });
    }

    public async Task SaveOcrScreenshotTextAsync(OcrServiceImageTextCacheRequest request, string data, int processRunId)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           INSERT INTO OcrScreenshotTextCache (Filename, OcrServiceName, PageNumber, Data, ProcessRunId, DateTimeUtc) 
                           VALUES (@Filename, @OcrServiceName, @PageNumber, @Data, @ProcessRunId, @DateTimeUtc)
                           """;

        await connection.ExecuteAsync(sql, new
        {
            Filename = request.Filepath,
            request.OcrServiceName,
            Data = data,
            request.PageNumber,
            ProcessRunId = processRunId,
            DateTimeUtc = DateTime.UtcNow
        });
    }

    public async Task ClearCacheAsync()
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           DELETE FROM ImageOnPage;
                           DELETE FROM NoOcrImagesMetadataCache;
                           DELETE FROM NoOcrPagesMetadataCache;
                           DELETE FROM NoOcrPageTextCache;
                           DELETE FROM OcrImageTextCache;
                           """;
        
        await connection.ExecuteAsync(sql);
    }

    public async Task ClearCacheAsync(string pdfFilename)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           DELETE FROM AllPagesText WHERE Filename = @Filename;
                           DELETE FROM ImageOnPage WHERE Filename = @Filename;
                           DELETE FROM NoOcrImagesMetadataCache WHERE Filename = @Filename;
                           DELETE FROM NoOcrPagesMetadataCache WHERE Filename = @Filename;
                           DELETE FROM NoOcrPageTextCache WHERE Filename = @Filename;
                           DELETE FROM OcrImageTextCache WHERE Filename = @Filename;
                           DELETE FROM PageScreenshot WHERE Filename = @Filename;
                           """;
        
        await connection.ExecuteAsync(sql, new
        {
            Filename = pdfFilename.Split('.')[0],
        });
    }

    public async Task UpdateProcessRunAsync(ProcessRun processRun)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           UPDATE ProcessRun 
                           SET EndDateTimeUtc = @EndDateTimeUtc 
                           WHERE ProcessRunId = @ProcessRunId
                           """;

        await connection.ExecuteAsync(sql, new
        {
            processRun.ProcessRunId,
            EndDateTimeUtc = DateTime.UtcNow
        });
    }

    public async Task UpdateLicenceSetLicenceAsync(LicenceSetLicence licenceSetLicence)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           UPDATE LicenceSetLicence 
                           SET LicenceId = @LicenceId 
                           WHERE LicenceSetId = @LicenceSetId 
                             AND LicenceNumber = @LicenceNumber 
                             AND ProcessRunId = @ProcessRunId
                           """;

        await connection.ExecuteAsync(sql, new
        {
            licenceSetLicence.LicenceSetId,
            licenceSetLicence.LicenceId,
            licenceSetLicence.LicenceNumber,
            licenceSetLicence.ProcessRunId,
        });
    }

    public async Task InsertLicenceSetLicenceAsync(int licenceSetId, int? licenceId, string? licenceNumber,
        string licenceVersionId,
        int processRunId)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           INSERT INTO LicenceSetLicence (LicenceSetId, LicenceId, LicenceNumber, LicenceVersionId, ProcessRunId, DateTimeUtc) 
                           VALUES (@LicenceSetId, @LicenceId, @LicenceNumber, @LicenceVersionId, @ProcessRunId, @DateTimeUtc)
                           """;

        await connection.ExecuteAsync(sql, new
        {
            LicenceSetId = licenceSetId,
            LicenceId = licenceId,
            LicenceNumber = licenceNumber ?? "UNKNOWN",
            LicenceVersionId = licenceVersionId,
            ProcessRunId = processRunId,
            DateTimeUtc = DateTime.UtcNow
        });
    }

    public async Task SaveLicenceSetTypeAsync(int licenceSetId, int licenceSetType, int processRunId)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           INSERT INTO LicenceSetType (LicenceSetId, LicenceSetType) 
                           VALUES (@LicenceSetId, @LicenceSetType)
                           """;

        await connection.ExecuteAsync(sql, new
        {
            LicenceSetId = licenceSetId,
            LicenceSetType = licenceSetType
        });
    }

    public async Task SaveAggregateSetAsync(int licenceSetId, string? aggregateSetId, string data, int processRunId)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           INSERT INTO AggregateSet (LicenceSetId, SchemaAggregateSetId, Data, ProcessRunId, DateTimeUtc)
                           VALUES (@LicenceSetId, @SchemaAggregateSetId, @Data, @ProcessRunId, @DateTimeUtc)
                           """;

        await connection.ExecuteAsync(sql, new
        {
            LicenceSetId = licenceSetId,
            SchemaAggregateSetId = aggregateSetId,
            Data = data,
            ProcessRunId = processRunId,
            DateTimeUtc = DateTime.UtcNow
        });
    }

    private NpgsqlConnection GetPostgresConnection()
        => dataSourceProvider.DataSource.CreateConnection();
}