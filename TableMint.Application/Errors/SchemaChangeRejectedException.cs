namespace TableMint.Application.Errors;

public sealed class SchemaChangeRejectedException(int affectedRecordCount)
    : Exception("The schema change would invalidate existing records.")
{
    public int AffectedRecordCount { get; } = affectedRecordCount;
}
