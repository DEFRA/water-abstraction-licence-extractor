using FluentAssertions;
using Moq;
using WALE.ProcessFile.RuleEngine.Interfaces;
using WALE.ProcessFile.RuleEngine.Models;
using WALE.ProcessFile.RuleEngine.Services;
using WALE.ProcessFile.Services.Configuration;
using WALE.ProcessFile.Services.Interfaces;
using WALE.ProcessFile.Services.Models;
using Xunit;

namespace WALE.ProcessFile.RuleEngine.Tests.Services;

public class FileTypeIdentifierServiceTests
{
    private readonly Mock<IPdfDataExtractorService> _mockPdfExtractorService;
    private readonly FileTypeIdentifierService _service;

    public FileTypeIdentifierServiceTests()
    {
        _mockPdfExtractorService = new Mock<IPdfDataExtractorService>();
        _service = new FileTypeIdentifierService(_mockPdfExtractorService.Object);
    }

    [Fact]
    public void Constructor_WithNullPdfExtractor_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new FileTypeIdentifierService((IPdfDataExtractorService)null!));
    }

    [Fact]
    public void IdentifyFileType_WithLicenseContent_ShouldReturnLicenseType()
    {
        // Arrange
        var content = "This document contains license and permit information";

        // Act
        var result = _service.IdentifyFileType(content);

        // Assert
        result.Should().NotBeNull();
        result!.FileType.Should().Be("License");
    }

    [Fact]
    public void IdentifyFileType_WithAddendumContent_ShouldReturnAddendumType()
    {
        // Arrange
        var content = "This addendum modifies the agreement";

        // Act
        var result = _service.IdentifyFileType(content);

        // Assert
        result.Should().NotBeNull();
        result!.FileType.Should().Be("Addendum");
    }

    [Fact]
    public void IdentifyFileType_WithNoMatchingContent_ShouldReturnNull()
    {
        // Arrange
        var content = "This is a regular document";

        // Act
        var result = _service.IdentifyFileType(content);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task IdentifyFileTypeAsync_WithNonExistentFile_ShouldThrowFileNotFoundException()
    {
        // Arrange
        var configuration = CreateTestConfiguration();
        var nonExistentFile = "non-existent-file.pdf";

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(() => 
            _service.IdentifyFileTypeAsync(nonExistentFile, configuration));
    }

    [Fact]
    public async Task IdentifyFileTypeAsync_WithValidPdfFile_ShouldReturnFileType()
    {
        // Arrange
        var configuration = CreateTestConfiguration();
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, "temp content");

        var mockMatchesResult = CreateMockMatchesResult("This document contains license information");
        _mockPdfExtractorService.Setup(x => x.GetMatchesAsync(It.IsAny<string>(), It.IsAny<LookupConfiguration>(), It.IsAny<List<string>>()))
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
            new List<(string LabelGroupName, List<LabelToMatch> Labels)>(),
            new Dictionary<string, string>(),
            Path.GetTempPath(),
            Path.GetTempPath());
    }

    private static MatchesResult CreateMockMatchesResult(string content)
    {
        var documentLine = new DocumentLine();
        var documentLineColumn = new DocumentLineColumn(content, new List<DocumentLineWord>());
        documentLine.Columns.Add(documentLineColumn);

        var labelGroupResult = new LabelGroupResult
        {
            Text = new List<DocumentLine> { documentLine },
            SubResults = new List<LabelGroupResult>()
        };

        return new MatchesResult
        {
            Matches = new List<LabelGroupResult> { labelGroupResult }
        };
    }
}
