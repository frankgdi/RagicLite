namespace TableMint.Domain.Tables;

public sealed class TableSchema
{
    public TableSchema(int version, IReadOnlyList<FieldDefinition> fields)
    {
        Version = version;
        Fields = fields;
    }

    public int Version { get; }

    public IReadOnlyList<FieldDefinition> Fields { get; }
}
