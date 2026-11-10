using WALE.ProcessFile.Services.Services;
using Xunit;

namespace WALE.ProcessFile.Services.Tests.UnitTests;

public class LocalFileServiceChunkTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), $"chunk-tests-{Guid.NewGuid()}");

    private LocalFileService Service()
    {
        Directory.CreateDirectory(_folder);
        return new LocalFileService(_folder + Path.DirectorySeparatorChar);
    }

    private string WriteFile(int length)
    {
        Directory.CreateDirectory(_folder);
        var name = $"{length}.bin";
        File.WriteAllBytes(
            Path.Combine(_folder, name),
            Enumerable.Range(0, length).Select(i => (byte)(i % 251)).ToArray());

        return name;
    }

    /// Mirrors ApiFileService's 413 fallback: read until a chunk comes back short.
    private static async Task<List<byte>> ReadAllChunksAsync(
        LocalFileService service, string filename, int chunkSize)
    {
        var all = new List<byte>();
        var index = 0;
        int read;

        do
        {
            var chunk = await service.GetFileAsBytesAsync(filename, index++, chunkSize);
            read = chunk.Length;
            all.AddRange(chunk);
        }
        while (read == chunkSize);

        return all;
    }

    [Fact]
    public async Task ChunksReassembleToTheWholeFile()
    {
        var service = Service();
        var filename = WriteFile(2500);

        var reassembled = await ReadAllChunksAsync(service, filename, 1000);

        Assert.Equal(File.ReadAllBytes(Path.Combine(_folder, filename)), reassembled);
    }

    [Fact]
    public async Task AFileThatIsAnExactMultipleOfTheChunkSizeTerminates()
    {
        var service = Service();
        var filename = WriteFile(2000);

        var reassembled = await ReadAllChunksAsync(service, filename, 1000);

        Assert.Equal(2000, reassembled.Count);
        Assert.Empty(await service.GetFileAsBytesAsync(filename, 2, 1000));
    }

    [Fact]
    public async Task AChunkPastTheEndIsEmptyRatherThanThrowing()
    {
        var service = Service();
        var filename = WriteFile(10);

        Assert.Empty(await service.GetFileAsBytesAsync(filename, 5, 1000));
    }

    [Fact]
    public async Task TheLastChunkIsShortNotPadded()
    {
        var service = Service();
        var filename = WriteFile(2500);

        Assert.Equal(500, (await service.GetFileAsBytesAsync(filename, 2, 1000)).Length);
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }
}
