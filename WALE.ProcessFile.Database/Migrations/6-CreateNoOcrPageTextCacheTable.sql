CREATE TABLE NoOcrPageTextCache (
    NoOcrPageTextCacheId int IDENTITY(1,1) NOT NULL,
    ProcessRunId int NOT NULL,
    Filename NVARCHAR(MAX) NOT NULL,
    PageNumber INT NOT NULL,
    NoOcrServiceName NVARCHAR(MAX) NOT NULL,
    Data TEXT NOT NULL,
    DateTimeUtc DATETIME2 NOT NULL
)