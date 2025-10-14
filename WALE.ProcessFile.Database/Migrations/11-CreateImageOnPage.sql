CREATE TABLE ImageOnPage (
    ImageOnPageId int IDENTITY(1,1) NOT NULL,
    [Filename] NVARCHAR(MAX) NOT NULL,
    NoOcrServiceName NVARCHAR(MAX) NOT NULL,
    Data varbinary(max) NOT NULL,
    PageNumber INT NOT NULL,
    ImageNumber INT NOT NULL,
    Extension NVARCHAR(5) NOT NULL,
    DateTimeUtc DATETIME2 NOT NULL
)