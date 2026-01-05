using Dapper;
using Tesseract;
using WALE.ProcessFile.Database.PostgreSQL.Services;
using WALE.Tools.Config;

namespace WALE.Tools;

public static class PopulateCachedImageWidthAndHeights
{
    private static readonly NpgsqlDataSourceProvider NpgsqlDataSourceProvider = new(KeyConfig.PostgresConnectionString);
    
    public static async Task PopulateWidthAndHeightsAsync()
    {
        Console.WriteLine("Started populating image width and heights");

        var list = await GetDataAsync();
        var totalUpdated = 0;

        while (list.Count > 0)
        {
            await GetAndUpdateWidthsAndHeights(list);
            totalUpdated += 100;
            
            Console.WriteLine($"Updated 100 - Total {totalUpdated}");
            
            list = await GetDataAsync();
        }

        Console.WriteLine("Finished populating image width and heights");
    }

    private static async Task GetAndUpdateWidthsAndHeights(List<(int Id, byte[] Bytes)> list)
    {
        foreach (var item in list)
        {
            var (width, height) = GetWidthAndHeight(item.Bytes);
            await UpdateWidthAndHeight(item.Id, width, height);

        }
    }

    private static async Task UpdateWidthAndHeight(int imageId, int width, int height)
    {
        const string sql = 
            @"UPDATE public.image_on_page
            SET width = @Width, height = @Height
            WHERE image_on_page_id = @Id";
        
        await using var connection = NpgsqlDataSourceProvider.DataSource.CreateConnection();
        
        await connection.ExecuteAsync(
            sql,
            new
            {
                Width = width,
                Height = height,
                Id = imageId
            });
    }
    
    private static async Task<List<(int Id, byte[] Bytes)>> GetDataAsync()
    {
        const string sql = 
            @"SELECT
                    image_on_page_id
                    , data
                FROM public.image_on_page
                where
                    width is null
                limit 100";
        
        await using var connection = NpgsqlDataSourceProvider.DataSource.CreateConnection();
        return (await connection.QueryAsync<(int, byte[])>(sql)).ToList();
    }
    
    private static (int Width, int Height) GetWidthAndHeight(byte[] bytes)
    {
        var pix = Pix.LoadFromMemory(bytes);
        return (pix.Width, pix.Height);
    }
}