namespace WALE.Tools._2ndHalf.ImportNaldData;

public class ForeignKeyDefinition
{
    public string ConstraintName { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public string ColumnNames { get; set; } = string.Empty;
    public string ReferencedTableName { get; set; } = string.Empty;
    public string ReferencedColumnNames { get; set; } = string.Empty;
    public string OnDeleteAction { get; set; } = string.Empty;
}