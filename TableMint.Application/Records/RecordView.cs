using System.Text.Json;

namespace TableMint.Application.Records;

public sealed record RecordView(
    Guid Id,
    Guid TenantId,
    Guid TableId,
    JsonElement Data,
    int SchemaVersion,
    int Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
