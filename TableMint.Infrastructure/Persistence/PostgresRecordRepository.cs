using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TableMint.Application.Abstractions;
using TableMint.Application.Errors;
using DomainRecord = TableMint.Domain.Tables.Record;

namespace TableMint.Infrastructure.Persistence;

public sealed class PostgresRecordRepository(AppDbContext dbContext)
    : IRecordRepository
{
    public async Task AddAsync(
        DomainRecord record,
        CancellationToken cancellationToken)
    {
        dbContext.Records.Add(ToRow(record));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<DomainRecord?> GetAsync(
        Guid tenantId,
        Guid tableId,
        Guid recordId,
        CancellationToken cancellationToken)
    {
        var row = await dbContext.Records
            .AsNoTracking()
            .SingleOrDefaultAsync(
                record =>
                    record.TenantId == tenantId &&
                    record.TableId == tableId &&
                    record.Id == recordId &&
                    record.DeletedAt == null,
                cancellationToken);

        return row is null ? null : ToDomain(row);
    }

    public async Task<IReadOnlyList<DomainRecord>> ListAsync(
        Guid tenantId,
        Guid tableId,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.Records
            .AsNoTracking()
            .Where(record =>
                record.TenantId == tenantId &&
                record.TableId == tableId &&
                record.DeletedAt == null)
            .ToListAsync(cancellationToken);

        return rows.Select(ToDomain).ToArray();
    }

    public async Task<RecordPage> QueryAsync(
        Guid tenantId,
        Guid tableId,
        RecordQuery query,
        CancellationToken cancellationToken)
    {
        var records = dbContext.Records
            .AsNoTracking()
            .Where(record =>
                record.TenantId == tenantId &&
                record.TableId == tableId &&
                record.DeletedAt == null);
        if (query.FilterFieldKey is not null && query.FilterValue is not null)
        {
            var filter = JsonSerializer.SerializeToElement(
                new Dictionary<string, JsonElement>
                {
                    [query.FilterFieldKey] = query.FilterValue.Value.Clone(),
                });
            records = records.Where(record =>
                EF.Functions.JsonContains(record.Data, filter));
        }

        var totalCount = await records.CountAsync(cancellationToken);
        var ordered = (query.SortBy, query.SortDirection) switch
        {
            (RecordSortBy.CreatedAt, SortDirection.Ascending) =>
                records.OrderBy(record => record.CreatedAt).ThenBy(record => record.Id),
            (RecordSortBy.CreatedAt, SortDirection.Descending) =>
                records.OrderByDescending(record => record.CreatedAt)
                    .ThenByDescending(record => record.Id),
            (RecordSortBy.UpdatedAt, SortDirection.Ascending) =>
                records.OrderBy(record => record.UpdatedAt).ThenBy(record => record.Id),
            (RecordSortBy.UpdatedAt, SortDirection.Descending) =>
                records.OrderByDescending(record => record.UpdatedAt)
                    .ThenByDescending(record => record.Id),
            _ => throw new ArgumentOutOfRangeException(nameof(query)),
        };
        var rows = await ordered
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new RecordPage(
            rows.Select(ToDomain).ToArray(),
            totalCount);
    }

    public async Task UpdateAsync(
        DomainRecord record,
        CancellationToken cancellationToken)
    {
        var data = record.Data.Clone();
        var expectedVersion = record.Version - 1;
        var affected = await dbContext.Records
            .Where(stored =>
                stored.Id == record.Id &&
                stored.TenantId == record.TenantId &&
                stored.Version == expectedVersion)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(stored => stored.Data, data)
                    .SetProperty(stored => stored.SchemaVersion, record.SchemaVersion)
                    .SetProperty(stored => stored.Version, record.Version)
                    .SetProperty(stored => stored.UpdatedAt, record.UpdatedAt)
                    .SetProperty(stored => stored.DeletedAt, record.DeletedAt),
                cancellationToken);

        if (affected == 0)
        {
            throw new ConflictException(
                "record_version_conflict",
                "The record was modified by another request.");
        }
    }

    private static RecordRow ToRow(DomainRecord record) =>
        new()
        {
            Id = record.Id,
            TenantId = record.TenantId,
            TableId = record.TableId,
            Data = record.Data.Clone(),
            SchemaVersion = record.SchemaVersion,
            Version = record.Version,
            CreatedAt = record.CreatedAt,
            UpdatedAt = record.UpdatedAt,
            DeletedAt = record.DeletedAt,
        };

    private static DomainRecord ToDomain(RecordRow row) =>
        DomainRecord.Rehydrate(
            row.Id,
            row.TenantId,
            row.TableId,
            row.Data,
            row.SchemaVersion,
            row.Version,
            row.CreatedAt,
            row.UpdatedAt,
            row.DeletedAt);
}
