using TableMint.Domain.Tables;

namespace TableMint.Application.Abstractions;

public interface ITableRepository
{
    Task AddAsync(Table table, CancellationToken cancellationToken);

    Task<Table?> GetAsync(
        Guid tenantId,
        Guid tableId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Table>> ListAsync(
        Guid tenantId,
        CancellationToken cancellationToken);

    Task UpdateAsync(Table table, CancellationToken cancellationToken);
}
