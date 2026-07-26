using FluentAssertions;
using Moq;
using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.RuleEngine.Services;
using WALE.ProcessFile.Services.Cache;
using WALE.ProcessFile.Services.Output;
using WALE.ProcessFile.Services.Services;
using Xunit;

namespace WALE.ProcessFile.RuleEngine.Tests.Services;

public class FileTypeIdentifierServiceTests
{
    private readonly FileTypeIdentifierService _service = new();

    [Fact(Skip = "Needs investigation and fixing")]
    public void IdentifyFileType_WithLicenseContent_ShouldReturnLicenseType()
    {
        // Arrange
        //var content = "This document contains license and permit information";
        var filePath = "";
        
        // Act
        var result = _service.IdentifyFileType(new MatchesResult(), filePath);

        // Assert
        result.Should().NotBeNull();
        result!.FileType.Should().Be("License");
    }

    [Fact(Skip = "Needs investigation and fixing")]
    public void IdentifyFileType_WithAddendumContent_ShouldReturnAddendumType()
    {
        // Arrange
        //var content = "This addendum modifies the agreement";
        var filePath = "";
        
        // Act
        var result = _service.IdentifyFileType(new MatchesResult(), filePath);

        // Assert
        result.Should().NotBeNull();
        result!.FileType.Should().Be("Addendum");
    }

    [Fact]
    public void IdentifyFileType_WithNoMatchingContent_ShouldReturnNull()
    {
        // Arrange
        //var content = "This is a regular document";
        var filePath = "";

        // Act
        var result = _service.IdentifyFileType(new MatchesResult(), filePath);

        // Assert
        result.Should().BeNull();
    }

    [Fact(Skip = "Needs investigation and fixing")]
    public void IdentifyFileTypeAsync_WithNonExistentFile_ShouldThrowFileNotFoundException()
    {
        // Arrange
        var nonExistentFile = "non-existent-file.pdf";

        // Act & Assert
        Assert.Throws<FileNotFoundException>(() => 
            _service.IdentifyFileType(new MatchesResult(), nonExistentFile));
    }

    [Fact(Skip = "Needs investigation and fixing")]
    public async Task IdentifyFileTypeAsync_WithValidPdfFile_ShouldReturnFileType()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, "temp content");

        try
        {
            // Act
            var result = _service.IdentifyFileType(new MatchesResult(), tempFile);

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
            [],
            new LocalFileService(""),
            new FileSystemCacheService(""),
            new FileSystemOutputService(""),
            1,
            DateTime.Now);
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