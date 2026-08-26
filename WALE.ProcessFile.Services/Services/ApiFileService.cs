using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Database.PostgreSQL.Helpers;
using WALE.ProcessFile.Services.Types;

namespace WALE.ProcessFile.Services.Services;

public class ApiFileService(HttpClient httpClient) : IFileService
{
    public async Task<List<string>> GetAllFilesAsync()
    {
        var dtStart = DateTime.UtcNow;
        ConsoleHelper.WriteLine($"INFO - {nameof(ApiFileService)} - Started getting files");
        
        var path = "/BFF/Files/ListAll";
       
        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.GetAsync(new Uri(httpClient.BaseAddress!, path)));
        
        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadAsStringAsync();
        
        var list = JsonSerializer.Deserialize<List<string>>(
            content,
            JsonHelper.GetSerializerOptions())!;
        
        var tsDuration = (DateTime.UtcNow - dtStart).TotalSeconds;
        ConsoleHelper.WriteLine($"INFO - {nameof(ApiFileService)} - Finished getting {list.Count} files in {tsDuration} seconds");
        
        // TOOD get this stuff from a DB table
        
        return list;
    }

    public async Task<List<FileMetadata>> GetAllFilesWithMetadataAsync(string startAfter, int take)
    {
        var path = $"/BFF/Files/ListAllWithMetadata?startAfter={startAfter}&take={take}";
       
        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.GetAsync(new Uri(httpClient.BaseAddress!, path)));
        
        var content = await response.Content.ReadAsStringAsync();
        response.EnsureSuccessStatusCode();
        
        return JsonSerializer.Deserialize<List<FileMetadata>>(
            content,
            JsonHelper.GetSerializerOptions())!;
    }

    public async Task<Stream?> GetFileAsStreamAsync(string filename)
    {
        try
        {
            var path = $"/Extractor/Files/Get?filename={filename}";

            var response = await HttpHelper.RateLimiter.Enqueue(() =>
                httpClient.GetAsync(path));

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStreamAsync();
        }
        catch (HttpRequestException hrex)
        {
            if (hrex.StatusCode == HttpStatusCode.RequestEntityTooLarge)
            {
                const int chunkSize = 5 * 1024 * 1024; // 5MB

                var loopBytes = -1;
                var totalLength = 0;
                
                var first = true;
                var loopIdx = 0;
                
                var documentChunks = new List<byte[]>();
                
                while (first || loopBytes == chunkSize)
                {
                    first = false;
                    
                    var bytes = await GetFileAsBytesAsync(filename, loopIdx++, chunkSize);

                    loopBytes = bytes.Length;
                    totalLength += loopBytes;
                    
                    documentChunks.Add(bytes);                    
                }

                var combinedByteArray = documentChunks[0].AsEnumerable();

                for (var idx = 1; idx < documentChunks.Count; idx++)
                {
                    combinedByteArray = combinedByteArray.Concat(documentChunks[idx]);
                }

                return new ByteStream(combinedByteArray, totalLength);
            }

            throw;
        }
    }
    
    public async Task<byte[]> GetFileAsBytesAsync(string filename, int chunkIndex, int chunkSize)
    {
        var path = $"/Extractor/Files/Get?filename={filename}&chunkIndex={chunkIndex}&chunkSize={chunkSize}";
        
        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.GetAsync(path));
        
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsByteArrayAsync();
    }

    public async Task UploadFileAsStreamAsync(string filename, Stream stream)
    {
        var path = "/BFF/Files/Upload";
        var uri = new Uri(httpClient.BaseAddress!, path);

        using var form = new MultipartFormDataContent();
        var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/pdf");
        form.Add(fileContent, "file", filename);

        var response = await httpClient.PutAsync(uri, form);
        response.EnsureSuccessStatusCode();
    }

    public async Task<string?> UploadFileChunkAsync(
        string filename,
        Stream stream,
        int chunkIndex,
        int totalChunks,
        string? uploadId = null)
    {
        var path = "/BFF/Files/UploadChunk";
        var uri = new Uri(httpClient.BaseAddress!, path);

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(filename), "filename");
        form.Add(new StringContent(chunkIndex.ToString()), "chunkIndex");
        form.Add(new StringContent(totalChunks.ToString()), "totalChunks");
        form.Add(new StringContent(uploadId ?? string.Empty), "uploadId");
        
        var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/octet-stream");
        form.Add(fileContent, "file", filename);

        var response = await httpClient.PutAsync(uri, form);
        var content = await response.Content.ReadAsStringAsync();
        
        response.EnsureSuccessStatusCode();
        return content;
    }

    public string FolderPath { get; set; } = "N/A";
    
    public async Task DeleteAsync(string filename)
    {
        var path = $"/BFF/Files/Delete?filename={filename}";
       
        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.DeleteAsync(new Uri(httpClient.BaseAddress!, path)));
        
        response.EnsureSuccessStatusCode();
    }

    public async Task<bool> ExistsAsync(string filename)
    {
        var path = $"/Extractor/Files/Exists?filename={filename}";
        
        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.GetAsync(path));
        
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        return "true".Equals(content, StringComparison.OrdinalIgnoreCase);
    }

    public async Task RenameAsync(string originalFilename, string newFilename)
    {
        var path = "/BFF/Files/Rename";
       
        var json = JsonSerializer.Serialize(new
        {
            originalFilename,
            newFilename
        }, JsonHelper.GetSerializerOptions());
        
        var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), httpContent));
        response.EnsureSuccessStatusCode();
    }

    public async Task<string> GetPresignedUrlAsync(string filename)
    {
        var path = $"/BFF/Files/Get?filename={filename}";
       
        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.GetAsync(new Uri(httpClient.BaseAddress!, path)));
        
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }
}