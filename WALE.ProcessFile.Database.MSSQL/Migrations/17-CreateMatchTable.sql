CREATE TABLE Match (
    MatchId int IDENTITY(1,1) NOT NULL,
    MatchesResultId int NOT NULL,
    LabelName nvarchar(max) NULL,
    LabelGroupName nvarchar(max) NULL,    
    Data TEXT NOT NULL
)