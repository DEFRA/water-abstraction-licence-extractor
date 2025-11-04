CREATE TABLE LicenceSet (
    LicenceSetId int IDENTITY(1,1) NOT NULL,
    ProcessRunId int NOT NULL,
    SchemaLicenceSetId NVARCHAR(MAX) NOT NULL,
    ShortLicenceSetId NVARCHAR(MAX) NOT NULL,
    DateTimeUtc DATETIME2 NOT NULL
)