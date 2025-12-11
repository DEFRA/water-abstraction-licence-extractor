using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using Npgsql;
using WALE.ProcessFile.Core.Enums.OutputSchema;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OutputSchema;
using WALE.ProcessFile.Core.Models.OutputSchema.Table;

namespace WALE.ProcessFile.Database.PostgreSQL.Services;

public class PostgresReadService(PostgresDataSourceProvider dataSourceProvider)
    : IDatabaseReadService
{
    public async Task<string?> GetNoOcrPagesMetadataAsync(NoOcrServiceMetadataCacheRequest request)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT Response 
                           FROM NoOcrPagesMetadataCache 
                           WHERE Filename = @Filename 
                             AND NoOcrServiceName = @NoOcrServiceName
                           LIMIT 1;
                           """;

        return await connection.QuerySingleOrDefaultAsync<string>(sql, new
        {
            Filename = request.Filepath,
            request.NoOcrServiceName
        });
    }

    public async Task<byte[]?> GetPageScreenshotAsync(int pageNumber, string fileName, string noOcrServiceName)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT Data 
                           FROM PageScreenshot 
                           WHERE Filename = @Filename 
                               AND NoOcrServiceName = @NoOcrServiceName 
                               AND PageNumber = @PageNumber
                           LIMIT 1;
                           """;

        return await connection.QuerySingleOrDefaultAsync<byte[]>(sql, new
        {
            Filename = fileName,
            NoOcrServiceName = noOcrServiceName,
            PageNumber = pageNumber,
        });
    }

    public async Task<string?> GetNoOcrPageTextLinesAsync(NoOcrServicePageCacheRequest request)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT Data 
                           FROM NoOcrPageTextCache 
                           WHERE Filename = @Filename 
                             AND PageNumber = @PageNumber 
                             AND NoOcrServiceName = @NoOcrServiceName
                           LIMIT 1;
                           """;

        return await connection.QuerySingleOrDefaultAsync<string>(sql, new
        {
            Filename = request.Filepath,
            request.PageNumber,
            request.NoOcrServiceName
        });
    }

    public async Task<string?> GetAllPagesTextAsync(string pdfFilename, string noOcrServiceName)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT Data 
                           FROM AllPagesText
                           WHERE Filename = @Filename
                             AND NoOcrServiceName = @NoOcrServiceName
                           LIMIT 1;
                           """;

        return await connection.QuerySingleOrDefaultAsync<string>(sql, new
        {
            Filename = pdfFilename,
            NoOcrServiceName = noOcrServiceName
        });
    }

    public async Task<string?> GetNoOcrImagesMetadata(NoOcrServiceMetadataCacheRequest request)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT Response 
                           FROM NoOcrImagesMetadataCache
                           WHERE Filename = @Filename 
                             AND NoOcrServiceName = @NoOcrServiceName
                           LIMIT 1;
                           """;

        return await connection.QuerySingleOrDefaultAsync<string>(sql, new
        {
            Filename = request.Filepath,
            request.NoOcrServiceName
        });
    }

    public async Task<string?> GetOcrImageTextAsync(OcrServiceImageTextCacheRequest request)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT Data 
                           FROM OcrImageTextCache 
                           WHERE Filename = @Filename 
                             AND OcrServiceName = @OcrServiceName 
                             AND PageNumber = @PageNumber 
                             AND ImageNumber = @ImageNumber
                           LIMIT 1;
                           """;

        return await connection.QuerySingleOrDefaultAsync<string>(sql, new
        {
            Filename = request.Filepath,
            request.OcrServiceName,
            request.PageNumber,
            request.ImageNumber
        });
    }

    public async Task<string?> GetOcrScreenshotTextAsync(OcrServiceImageTextCacheRequest request)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT Data 
                           FROM OcrScreenshotTextCache 
                           WHERE Filename = @Filename
                             AND OcrServiceName = @OcrServiceName
                             AND PageNumber = @PageNumber
                           LIMIT 1;
                           """;

        return await connection.QuerySingleOrDefaultAsync<string>(sql, new
        {
            Filename = request.Filepath,
            request.OcrServiceName,
            request.PageNumber,
        });
    }

    public async Task<byte[]?> GetImageBytesAsync(OcrServiceImageDataCacheRequest request)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT Data 
                           FROM ImageOnPage 
                           WHERE Filename = @Filename 
                             AND NoOcrServiceName = @NoOcrServiceName 
                             AND PageNumber = @PageNumber 
                             AND ImageNumber = @ImageNumber 
                             AND Extension = @Extension
                           LIMIT 1;
                           """;

        return await connection.QuerySingleOrDefaultAsync<byte[]>(sql, new
        {
            Filename = request.Filepath,
            request.NoOcrServiceName,
            request.PageNumber,
            request.ImageNumber,
            request.Extension
        });
    }

    public async Task<List<(int imageNumber, string extension)>> GetImagesAsync(OcrServiceImageDataCacheRequest request)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT ImageNumber, Extension 
                           FROM ImageOnPage 
                           WHERE Filename = @Filename 
                             AND PageNumber = @PageNumber
                           """;

        var results = await connection.QueryAsync<(int, string)>(sql, new
        {
            Filename = request.Filepath,
            request.PageNumber,
        });

        return results.ToList();
    }

    public async Task<List<ProcessRun>> GetProcessRunsAsync()
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT 
                               ProcessRunId, 
                               Description, 
                               StartDateTimeUtc, 
                               EndDateTimeUtc, 
                               NumberOfFiles 
                           FROM ProcessRun
                           """;

        return (await connection.QueryAsync<ProcessRun>(sql)).ToList();
    }

    public async Task<ProcessRun?> GetMostRecentProcessRunAsync(string filename)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT 
                               Licence.ProcessRunId, 
                               ProcessRun.Description, 
                               ProcessRun.StartDateTimeUtc, 
                               ProcessRun.EndDateTimeUtc, 
                               ProcessRun.NumberOfFiles 
                           FROM Licence 
                           JOIN ProcessRun 
                               ON Licence.ProcessRunId = ProcessRun.ProcessRunId 
                           WHERE Filename = @Filename 
                           ORDER BY Licence.ProcessRunId DESC
                           LIMIT 1;
                           """;

        return await connection.QuerySingleOrDefaultAsync<ProcessRun>(sql, new
        {
            Filename = filename
        });
    }

    public async Task<List<Licence>> GetLicencesAsync(int processRunId)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT Data, LicenceId 
                           FROM Licence 
                           WHERE ProcessRunId = @ProcessRunId
                           """;

        var results = await connection.QueryAsync<(string Data, int LicenceId)>(sql, new
        {
            ProcessRunId = processRunId
        });

        return results.Select(r =>
        {
            var licence = JsonSerializer.Deserialize<Licence>(r.Data, GetSerializerOptions())!;
            licence.NoneSchemaData.TryAdd("licenceId", r.LicenceId);
            return licence;
        }).ToList();
    }

    public async Task<List<LicenceSetTable>> GetLicenceSetsSimpleAsync(int processRunId)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT 
                               LicenceSetId, 
                               ShortLicenceSetId, 
                               SchemaLicenceSetId 
                           FROM LicenceSet 
                           WHERE ProcessRunId = @ProcessRunId
                           """;

        return (await connection.QueryAsync<LicenceSetTable>(sql, new
        {
            ProcessRunId = processRunId
        })).ToList();
    }

    public async Task<List<LicenceSetTable>> GetLicenceSetsSimpleAsync(string filename, int processRunId)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT 
                               LicenceSetId,
                               ShortLicenceSetId, 
                               SchemaLicenceSetId 
                           FROM LicenceSet 
                           WHERE ProcessRunId = @ProcessRunId 
                             AND Filename = @Filename
                           """;

        return (await connection.QueryAsync<LicenceSetTable>(sql, new
        {
            ProcessRunId = processRunId,
            Filename = filename
        })).ToList();
    }

    public async Task<List<LicenceSetLicence>> GetLicenceSetLicencesAsync(int processRunId)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT 
                               LicenceId,
                               LicenceNumber,
                               LicenceVersionId,
                               LicenceSetId,
                               ProcessRunId
                           FROM LicenceSetLicence 
                           WHERE ProcessRunId = @ProcessRunId
                           """;

        return (await connection.QueryAsync<LicenceSetLicence>(sql, new
        {
            ProcessRunId = processRunId
        })).ToList();
    }

    public async Task<List<LicenceSetLicence>> GetLicenceSetLicencesAsync(int licenceSetId, int processRunId)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT 
                               LicenceId,
                               LicenceNumber,
                               LicenceVersionId,
                               LicenceSetId,
                               ProcessRunId
                           FROM LicenceSetLicence 
                           WHERE LicenceSetId = @LicenceSetId 
                             AND ProcessRunId = @ProcessRunId
                           """;
        
        return (await connection.QueryAsync<LicenceSetLicence>(sql, new
        {
            ProcessRunId = processRunId,
            LicenceSetId = licenceSetId
        })).ToList();
    }

    public async Task<LicenceSetType[]> GetLicenceSetTypes(int licenceSetId)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT LicenceSetType 
                           FROM LicenceSetType 
                           WHERE LicenceSetId = @LicenceSetId
                           """;

        return (await connection.QueryAsync<int>(sql, new { LicenceSetId = licenceSetId }))
            .Select(x => (LicenceSetType)x)
            .ToArray();
    }

    public async Task<List<(int LicenceSetId, LicenceSetType Type)>> GetLicenceSetTypesForProcessRun(int processRunId)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT
                            lst.LicenceSetId,
                            lst.LicenceSetType
                           FROM LicenceSetType lst
                           JOIN LicenceSet ls on ls.LicenceSetId = lst.LicenceSetId
                           WHERE ls.ProcessRunId = @ProcessRunId
                           """;

        var results = await connection.QueryAsync<(int LicenceSetId, int LicenceSetType)>(
            sql, new { ProcessRunId = processRunId });
        
        return results.Select(r => (r.LicenceSetId, (LicenceSetType)r.LicenceSetType)).ToList();
    }

    public async Task<AggregateSet[]?> GetAggregateSets(int licenceSetId)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT 
                               AggregateSetId,
                               SchemaAggregateSetId,
                               Data 
                           FROM AggregateSet 
                           WHERE LicenceSetId = @LicenceSetId
                           """;

        // This was not fully implemented in the SqlServerReadService, so I am skipping it for now.
        return [];
    }

    public async Task<List<(int LicenceSetId, AggregateSet AggregateSet)>> GetAggregateSetsForProcessRun(
        int processRunId)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT 
                               AggregateSetId,
                               SchemaAggregateSetId,
                               Data 
                           FROM AggregateSet 
                           WHERE ProcessRunId = @ProcessRunId
                           """;
        
        // This was not fully implemented in the SqlServerReadService, so I am skipping it for now.
        return [];
    }

    public async Task<Licence?> GetLicenceAsync(string filename)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT 
                               Data,
                               LicenceId 
                           FROM Licence
                           WHERE Filename = @Filename 
                           ORDER BY ProcessRunId DESC
                           LIMIT 1;
                           """;
        
        var result = await connection.QuerySingleOrDefaultAsync<(string Data, int LicenceId)?>(sql, new { Filename = filename });
        if (result == null)
        {
            return null;
        }
        
        var data = JsonSerializer.Deserialize<Licence>(result.Value.Data, GetSerializerOptions())!;
        data.NoneSchemaData.TryAdd("licenceId", result.Value.LicenceId);
        return data;
    }

    public async Task<Licence?> GetLicenceAsync(string licenceNumber, int processRunId)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT 
                               Data,
                               LicenceId 
                           FROM Licence
                           WHERE LicenceNumber = @LicenceNumber 
                             AND ProcessRunId = @ProcessRunId
                           LIMIT 1;
                           """;
        
        var result = await connection.QuerySingleOrDefaultAsync<(string Data, int LicenceId)?>(sql, new
        {
            LicenceNumber = licenceNumber,
            ProcessRunId = processRunId
        });
        
        if (result == null)
        {
            return null;
        }
        
        var data = JsonSerializer.Deserialize<Licence>(result.Value.Data, GetSerializerOptions())!;
        data.NoneSchemaData.TryAdd("licenceId", result.Value.LicenceId);
        return data;
    }

    public async Task<MatchesResult?> GetMatchesResult(string filename)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT Data 
                           FROM MatchesResult 
                           WHERE Filename = @Filename 
                           ORDER BY ProcessRunId DESC
                           LIMIT 1;
                           """;
        
        var result = await connection.QuerySingleOrDefaultAsync<string>(sql, new { Filename = filename });

        return result == null 
            ? null 
            : JsonSerializer.Deserialize<MatchesResult>(result, GetSerializerOptions());
    }

    private NpgsqlConnection GetPostgresConnection()
        => dataSourceProvider.DataSource.CreateConnection();

    // TODO move to a 'Core' layer
    private static JsonSerializerOptions GetSerializerOptions() =>
        new()
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