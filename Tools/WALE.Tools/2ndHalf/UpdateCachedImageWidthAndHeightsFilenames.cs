using Tesseract;
using WALE.ProcessFile.Database.PostgreSQL.Services;
using WALE.Tools.Config;

namespace WALE.Tools._2ndHalf;

public static class UpdateCachedImageWidthAndHeightsFilenames
{
    private static readonly NpgsqlDataSourceProvider NpgsqlDataSourceProvider = new(
        KeyConfig.PostgresHost,
        KeyConfig.PostgresPort,
        KeyConfig.PostgresDbName,
        KeyConfig.PostgresUsername,
        KeyConfig.PostgresPassword);
    
    public static async Task PopulateWidthAndHeightsAsync()
    {
        Console.WriteLine("Started updating filename image width and heights");

        var folderPaths = GetFolderPaths();
        var totalFoldersDone = 0;
        
        foreach (var folderPath in folderPaths)
        {
            var subPath = $"{folderPath}/PdfPig/Images";
            var anythingToDo = false;
            
            try
            {
                var files = Directory
                    .GetFiles(subPath)
                    .Select(f => f.Split('/').Last())
                    .Where(f => f.StartsWith("page-") && f.Contains("-image-"));

                foreach (var filename in files)
                {
                    var extensionParts = filename.Split('.');
                    var main = extensionParts[0];
                    var extension = extensionParts[1];
                
                    if (main.Contains('+'))
                    {
                        continue;
                    }

                    var fullOriginalFilename = $"{subPath}/{filename}";
                
                    var bytes = await File.ReadAllBytesAsync(fullOriginalFilename);
                    var (width, height) = GetWidthAndHeight(bytes);

                    if (width == -2)
                    {
                        File.Delete(fullOriginalFilename);
                        continue;
                    }
                    
                    var fullNewFileName = $"{subPath}/{main}+{width}+{height}.{extension}";
                
                    var fi = new FileInfo(fullOriginalFilename);
                    fi.MoveTo(fullNewFileName);

                    anythingToDo = true;
                }
            }
            catch (DirectoryNotFoundException)
            {
                continue;
            }

            if (anythingToDo)
            {
                Console.WriteLine($"1 folder done - {++totalFoldersDone} in total");   
            }
        }

        Console.WriteLine("Finished updating filename image width and heights");
    }
    
    private static (int Width, int Height) GetWidthAndHeight(byte[] bytes)
    {
        Pix pix;

        try
        {
            pix = Pix.LoadFromMemory(bytes);
        }
        catch (Exception)
        {
            return (-2, -2);
        }

        return (pix.Width, pix.Height);
    }
    
    private static string[] GetFolderPaths()
    {
        return Directory.GetDirectories("../../../../../WALE.ProcessFile.Services.Tests/bin/Debug/net9.0/Cache");   
    }
}