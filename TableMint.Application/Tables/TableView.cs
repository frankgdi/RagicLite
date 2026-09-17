namespace TableMint.Application.Tables;

public sealed record TableView(
    Guid Id,
    Guid TenantId,
    string Name,
    int SchemaVersion,
    int Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<FieldView> Fields);
