using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;

namespace WRADI.Services.LicenceFinder.Implementations;

public class InventoryApiClient : IInventoryApiClient
{
    public InventoryApiClient(string apiBaseUrl)
    {
        HttpClient = new HttpClient();
        HttpClient.BaseAddress = new Uri(apiBaseUrl);
    }
    
    private HttpClient HttpClient { get; set; }

    public async Task<List<FileMetadata>> GetAllWithMetadataAsync(string startAfter, int take)
    {
        var path = $"/BFF/Files/ListAllWithMetadata?startAfter={startAfter}&take={take}";

        var response = await HttpClient.GetAsync(path);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<FileMetadata>>(
            content,
            GetSerializerOptions())!;
    }
    
    // TODO - In time this should come from the other project as a NuGet reference
    private static JsonSerializerOptions GetSerializerOptions()
    {
        _options ??= new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters =
            {
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
            }
        };

        return _options;
    }
    
    private static JsonSerializerOptions? _options;
}