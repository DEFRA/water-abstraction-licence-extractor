namespace WALE.ProcessFile.Core.Models;

/// <summary>
/// Represents DMS (Document Management System) extract data that can be extracted from Excel or CSV files.
/// </summary>
public class DmsExtract
{
    /// <summary>
    /// Site Collection identifier
    /// </summary>
    public string SiteCollection { get; set; } = string.Empty;

    /// <summary>
    /// Name of the library
    /// </summary>
    public string LibraryName { get; set; } = string.Empty;

    /// <summary>
    /// Permit number associated with the document
    /// </summary>
    public string PermitNumber { get; set; } = string.Empty;

    /// <summary>
    /// Name of the file
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Size of the file
    /// </summary>
    public string FileSize { get; set; } = string.Empty;

    /// <summary>
    /// Type of the file
    /// </summary>
    public string FileType { get; set; } = string.Empty;

    /// <summary>
    /// Customer operator name
    /// </summary>
    public string CustomerOperatorName { get; set; } = string.Empty;

    /// <summary>
    /// Name of the facility
    /// </summary>
    public string FacilityName { get; set; } = string.Empty;

    /// <summary>
    /// Address of the facility
    /// </summary>
    public string FacilityAddress { get; set; } = string.Empty;

    /// <summary>
    /// Postcode of the facility address
    /// </summary>
    public string FacilityAddressPostcode { get; set; } = string.Empty;

    /// <summary>
    /// Regime classification
    /// </summary>
    public string Regime { get; set; } = string.Empty;

    /// <summary>
    /// Activity class
    /// </summary>
    public string ActivityClass { get; set; } = string.Empty;

    /// <summary>
    /// Activity sub class
    /// </summary>
    public string ActivitySubClass { get; set; } = string.Empty;

    /// <summary>
    /// Type of permit
    /// </summary>
    public string TypeOfPermit { get; set; } = string.Empty;

    /// <summary>
    /// Catchment area
    /// </summary>
    public string Catchment { get; set; } = string.Empty;

    /// <summary>
    /// National security classification
    /// </summary>
    public string NationalSecurity { get; set; } = string.Empty;

    /// <summary>
    /// Disclosure status
    /// </summary>
    public string DisclosureStatus { get; set; } = string.Empty;
    
    /// <summary>
    /// Document date
    /// </summary>   
    public string DocumentDate { get; set; } = string.Empty;
    
    /// <summary>
    /// Upload date
    /// </summary>   
    public string UploadDate { get; set; } = string.Empty;
    
    /// <summary>
    /// File url
    /// </summary>   
    public string FileUrl { get; set; } = string.Empty;
    
    /// <summary>
    /// Other Reference
    /// </summary>   
    public string OtherReference { get; set; } = string.Empty;
    
    /// <summary>
    /// Modified date
    /// </summary>   
    public string ModifiedDate { get; set; } = string.Empty;
    
    /// <summary>
    /// File Id
    /// </summary>   
    public string FileId { get; set; } = string.Empty;

    public DmsExtract Clone()
    {
        return new DmsExtract
        {
            ActivityClass = ActivityClass,
            ActivitySubClass = ActivitySubClass,
            Catchment = Catchment,
            CustomerOperatorName = CustomerOperatorName,
            DisclosureStatus = DisclosureStatus,
            DocumentDate = DocumentDate,
            FacilityName = FacilityName,
            FacilityAddress = FacilityAddress,
            FacilityAddressPostcode = FacilityAddressPostcode,
            FileId = FileId,
            FileName = FileName,
            FileSize = FileSize,
            FileType = FileType,
            FileUrl = FileUrl,
            LibraryName = LibraryName,
            ModifiedDate = ModifiedDate,
            NationalSecurity = NationalSecurity,
            OtherReference = OtherReference,
            PermitNumber = PermitNumber,
            Regime = Regime,
            SiteCollection = SiteCollection,
            TypeOfPermit = TypeOfPermit,
            UploadDate = UploadDate
        };
    }
}