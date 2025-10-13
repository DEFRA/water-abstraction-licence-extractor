CREATE TABLE NoOcrPageTextCache (
    NoOcrPageTextCacheId int IDENTITY(1,1) NOT NULL,
    Filename NVARCHAR(MAX) NOT NULL,
    PageNumber INT NOT NULL,
    NoOcrServiceName NVARCHAR(MAX) NOT NULL,
    Data TEXT NOT NULL
)