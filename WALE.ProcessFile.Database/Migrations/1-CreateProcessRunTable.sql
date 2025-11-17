CREATE TABLE ProcessRun (
    ProcessRunId int IDENTITY(1,1) NOT NULL,
    [Description] NVARCHAR(MAX) NULL,
    StartDateTimeUtc datetime NOT NULL,
    EndDateTimeUtc datetime NULL,
    NumberOfFiles int NOT NULL
)