namespace WALE.ProcessFile.Core.Models;

public record NaldLinkedLicence
{
    public required NaldLicence NaldLicence { get; init; }
    public required NaldLinkedLicenceType LinkType { get; init; }
}