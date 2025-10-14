CREATE TABLE OcrImageTextCache (
    OcrImageTextCacheId int IDENTITY(1,1) NOT NULL,
    ProcessRunId int NOT NULL,
    Filename NVARCHAR(MAX) NOT NULL,
    PageNumber INT NOT NULL,
    ImageNumber INT NOT NULL,    
    OcrServiceName NVARCHAR(MAX) NOT NULL,
    Data TEXT NOT NULL,
    DateTimeUtc DATETIME2 NOT NULL
)