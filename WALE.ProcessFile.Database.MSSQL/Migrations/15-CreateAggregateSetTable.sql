CREATE TABLE AggregateSet (
    AggregateSetId int IDENTITY(1,1) NOT NULL,
    ProcessRunId int NOT NULL,    
    LicenceSetId int NOT NULL,
    SchemaAggregateSetId NVARCHAR(MAX) NOT NULL,
    Data NVARCHAR(MAX) NOT NULL,    
    DateTimeUtc DATETIME2 NOT NULL
)