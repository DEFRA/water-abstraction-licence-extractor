CREATE TABLE NoOcrImagesMetadataCache (
    NoOcrImagesMetadataCacheId int IDENTITY(1,1) NOT NULL,
    [Filename] NVARCHAR(MAX) NOT NULL,
    NoOcrServiceName NVARCHAR(MAX) NOT NULL,    
    Response TEXT NOT NULL,
    DateTimeUtc DATETIME2 NOT NULL
)