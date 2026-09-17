using TableMint.Domain.Tables;

namespace TableMint.Application.Abstractions;

public interface IRecordRepository
{
    Task AddAsync(Record record, CancellationToken cancellationToken);

    Task<Record?> GetAsync(
        Guid tenantId,
        Guid tableId,
        Guid recordId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Record>> ListAsync(
        Guid tenantId,
        Guid tableId,
        CancellationToken cancellationToken);

    Task<RecordPage> QueryAsync(
        Guid tenantId,
        Guid tableId,
        RecordQuery query,
        CancellationToken cancellationToken);

    Task UpdateAsync(Record record, CancellationToken cancellationToken);
}
