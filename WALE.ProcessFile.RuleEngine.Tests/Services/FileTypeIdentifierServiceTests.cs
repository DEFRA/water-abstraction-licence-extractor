using FluentAssertions;
using Moq;
using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.RuleEngine.Services;
using WALE.ProcessFile.Services.Cache;
using WALE.ProcessFile.Services.Services;
using Xunit;

namespace WALE.ProcessFile.RuleEngine.Tests.Services;

public class FileTypeIdentifierServiceTests
{
    private readonly Mock<IPdfDataExtractorService> _mockPdfExtractorService; // TODO remove Moq for FakeItEasy
    private readonly FileTypeIdentifierService _service;

    public FileTypeIdentifierServiceTests()
    {
        _mockPdfExtractorService = new Mock<IPdfDataExtractorService>();
        _service = new FileTypeIdentifierService([_mockPdfExtractorService.Object]);
    }

    [Fact]
    public void Constructor_WithNullPdfExtractor_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new FileTypeIdentifierService(null!));
    }

    [Fact(Skip = "Needs investigation and fixing")]
    public async Task IdentifyFileType_WithLicenseContent_ShouldReturnLicenseType()
    {
        // Arrange
        //var content = "This document contains license and permit information";
        var filePath = "";
        
        var lookupConfiguration = new LookupConfiguration(
            [],
            [],
            [],
            [],
            new LocalFileService(""),
            new FileSystemCacheService(""),
            1);

        // Act
        var result = await _service.IdentifyFileTypeAsync(filePath, lookupConfiguration);

        // Assert
        result.Should().NotBeNull();
        result!.FileType.Should().Be("License");
    }

    [Fact(Skip = "Needs investigation and fixing")]
    public async Task IdentifyFileType_WithAddendumContent_ShouldReturnAddendumType()
    {
        // Arrange
        //var content = "This addendum modifies the agreement";
        var filePath = "";
        
        var lookupConfiguration = new LookupConfiguration(
            [],
            [],
            [],
            [],
            new LocalFileService(""),
            new FileSystemCacheService(""),
            1);

        // Act
        var result = await _service.IdentifyFileTypeAsync(filePath, lookupConfiguration);

        // Assert
        result.Should().NotBeNull();
        result!.FileType.Should().Be("Addendum");
    }

    [Fact]
    public async Task IdentifyFileType_WithNoMatchingContent_ShouldReturnNull()
    {
        // Arrange
        //var content = "This is a regular document";
        var filePath = "";
        var lookupConfiguration = new LookupConfiguration(
            [],
            [],
            [],
            [],
            new LocalFileService(""),
            new FileSystemCacheService("Cache"),
            1);

        // Act
        var result = await _service.IdentifyFileTypeAsync(filePath, lookupConfiguration);

        // Assert
        result.Should().BeNull();
    }

    [Fact(Skip = "Needs investigation and fixing")]
    public async Task IdentifyFileTypeAsync_WithNonExistentFile_ShouldThrowFileNotFoundException()
    {
        // Arrange
        var configuration = CreateTestConfiguration();
        var nonExistentFile = "non-existent-file.pdf";

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(() => 
            _service.IdentifyFileTypeAsync(nonExistentFile, configuration));
    }

    [Fact(Skip = "Needs investigation and fixing")]
    public async Task IdentifyFileTypeAsync_WithValidPdfFile_ShouldReturnFileType()
    {
        // Arrange
        var configuration = CreateTestConfiguration();
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, "temp content");

        var mockMatchesResult = CreateMockMatchesResult("This document contains license information");
        _mockPdfExtractorService.Setup(x => x.GetMatchesAsync(
                It.IsAny<string>(),
                It.IsAny<DmsFileData>(),
                It.IsAny<LookupConfiguration>(),
                It.IsAny<List<string>>(),
                It.IsAny<int>()))
            .ReturnsAsync(mockMatchesResult);

        try
        {
            // Act
            var result = await _service.IdentifyFileTypeAsync(tempFile, configuration);

            // Assert
            result.Should().NotBeNull();
            result!.FileType.Should().Be("License");
        }
        finally
        {
            // Cleanup
            File.Delete(tempFile);
        }
    }

    private static LookupConfiguration CreateTestConfiguration()
    {
        return new LookupConfiguration(
            [],
            new Dictionary<string, DmsFileData>(),
            [],
            [],
            new LocalFileService(""),
            new FileSystemCacheService(""),
            1);
    }

    private static MatchesResult CreateMockMatchesResult(string content)
    {
        var documentLine = new DocumentLine();
        var documentLineColumn = new DocumentLineColumn([new(
            content,
            null,
            DocumentLineWordCoordinates.NotKnown(),
            null)]);
        
        documentLine.Columns.Add(documentLineColumn);

        var labelGroupResult = new LabelGroupResult
        {
            Text = new List<DocumentLine> { documentLine },
            SubResults = new List<LabelGroupResult>()
        };

        return new MatchesResult
        {
            Matches = [labelGroupResult]
        };
    }
}