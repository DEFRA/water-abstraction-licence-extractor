using Microsoft.Extensions.Logging.Abstractions;
using WRADI.ProcessFile.Local;

namespace WRADI.ProcessFile.Local.Tests;

public class DocumentTypeServiceProvidersTests
{
    [Fact]
    public void WhenDocumentTypeIsRecognised_ThenReturnsItsOwnProvider()
    {
        // Arrange
        var abstractionLicenceProvider = new FakeServiceProvider();
        var wrInspectionReportProvider = new FakeServiceProvider();

        var providers = new DocumentTypeServiceProviders(
            new Dictionary<string, IServiceProvider>
            {
                ["AbstractionLicence"] = abstractionLicenceProvider,
                ["WrInspectionReport"] = wrInspectionReportProvider
            });

        // Act
        var result = providers.Get("WrInspectionReport", NullLogger.Instance);

        // Assert
        Assert.Same(wrInspectionReportProvider, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("SomeUnrecognisedDocumentType")]
    public void WhenDocumentTypeIsMissingOrUnrecognised_ThenFallsBackToAbstractionLicence(string? documentType)
    {
        // Arrange
        var abstractionLicenceProvider = new FakeServiceProvider();
        var wrInspectionReportProvider = new FakeServiceProvider();

        var providers = new DocumentTypeServiceProviders(
            new Dictionary<string, IServiceProvider>
            {
                ["AbstractionLicence"] = abstractionLicenceProvider,
                ["WrInspectionReport"] = wrInspectionReportProvider
            });

        // Act
        var result = providers.Get(documentType, NullLogger.Instance);

        // Assert
        Assert.Same(abstractionLicenceProvider, result);
    }

    private sealed class FakeServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
