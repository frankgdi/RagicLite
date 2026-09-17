using System.Text.Json;

namespace TableMint.Infrastructure.Persistence;

internal sealed class RecordRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid TableId { get; set; }

    public JsonElement Data { get; set; }

    public int SchemaVersion { get; set; }

    public int Version { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}
