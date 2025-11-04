CREATE TABLE PageScreenshot (
    PageScreenshotId int IDENTITY(1,1) NOT NULL,
    ProcessRunId int NOT NULL,
    [Filename] NVARCHAR(MAX) NOT NULL,
    PageNumber INT NOT NULL,
    NoOcrServiceName NVARCHAR(MAX) NOT NULL,    
    Data varbinary(max) NOT NULL,
    DateTimeUtc DATETIME2 NOT NULL
)