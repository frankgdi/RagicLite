using System.Text.Json;

namespace TableMint.Domain.Tables;

public sealed class Record
{
    private Record(
        Guid id,
        Guid tenantId,
        Guid tableId,
        JsonElement data,
        int schemaVersion,
        int version,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        DateTimeOffset? deletedAt)
    {
        Id = id;
        TenantId = tenantId;
        TableId = tableId;
        Data = data.Clone();
        SchemaVersion = schemaVersion;
        Version = version;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        DeletedAt = deletedAt;
    }

    public Guid Id { get; }

    public Guid TenantId { get; }

    public Guid TableId { get; }

    public JsonElement Data { get; private set; }

    public int SchemaVersion { get; private set; }

    public int Version { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public static Record Create(
        Guid id,
        Guid tenantId,
        Guid tableId,
        JsonElement data,
        int schemaVersion,
        DateTimeOffset createdAt) =>
        new(
            id,
            tenantId,
            tableId,
            data,
            schemaVersion,
            version: 1,
            createdAt,
            updatedAt: createdAt,
            deletedAt: null);

    public static Record Rehydrate(
        Guid id,
        Guid tenantId,
        Guid tableId,
        JsonElement data,
        int schemaVersion,
        int version,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        DateTimeOffset? deletedAt) =>
        new(
            id,
            tenantId,
            tableId,
            data,
            schemaVersion,
            version,
            createdAt,
            updatedAt,
            deletedAt);

    public void Update(
        JsonElement data,
        int schemaVersion,
        DateTimeOffset updatedAt)
    {
        Data = data.Clone();
        SchemaVersion = schemaVersion;
        Version++;
        UpdatedAt = updatedAt;
    }

    public void Delete(DateTimeOffset deletedAt)
    {
        DeletedAt = deletedAt;
        Version++;
        UpdatedAt = deletedAt;
    }
}
