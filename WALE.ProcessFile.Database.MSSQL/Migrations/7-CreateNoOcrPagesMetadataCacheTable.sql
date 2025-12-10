CREATE TABLE NoOcrPagesMetadataCache (
    NoOcrPagesMetadataCacheId int IDENTITY(1,1) NOT NULL,
    ProcessRunId int NOT NULL,
    [Filename] NVARCHAR(MAX) NOT NULL,
    NoOcrServiceName NVARCHAR(MAX) NOT NULL,    
    Response TEXT NOT NULL,
    DateTimeUtc DATETIME2 NOT NULL
)