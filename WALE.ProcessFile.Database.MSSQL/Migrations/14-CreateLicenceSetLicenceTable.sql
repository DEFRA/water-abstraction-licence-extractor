CREATE TABLE LicenceSetLicence (
    LicenceSetLicenceId int IDENTITY(1,1) NOT NULL,
    ProcessRunId int NOT NULL,    
    LicenceSetId int NOT NULL,
    LicenceNumber NVARCHAR(MAX) NOT NULL,
    LicenceVersionId NVARCHAR(MAX) NOT NULL,    
    DateTimeUtc DATETIME2 NOT NULL
)