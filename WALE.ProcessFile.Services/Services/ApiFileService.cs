using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Database.PostgreSQL.Helpers;

namespace WALE.ProcessFile.Services.Services;

public class ApiFileService(HttpClient httpClient) : IFileService
{
    public async Task<List<string>> GetAllFilesAsync()
    {
        var path = "/BFF/Files/ListAll";
       
        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.GetAsync(new Uri(httpClient.BaseAddress!, path)));
        
        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadAsStringAsync();
        
        return JsonSerializer.Deserialize<List<string>>(
            content,
            JsonHelper.GetSerializerOptions())!;
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
                ConsoleHelper.WriteLine(
                    $"WARNING - {nameof(ApiFileService)} - File was too large, skipping - {filename}");
                
                return null;
            }

            throw;
        }
    }
    
    public async Task<byte[]> GetFileAsBytesAsync(string filename)
    {
        var path = $"/Extractor/Files/Get?filename={filename}";
        
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
}