using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TableMint.Application.Abstractions;
using TableMint.Application.Errors;
using TableMint.Domain.Tables;

namespace TableMint.Infrastructure.Persistence;

public sealed class PostgresTableRepository(AppDbContext dbContext)
    : ITableRepository
{
    public async Task AddAsync(
        Table table,
        CancellationToken cancellationToken)
    {
        dbContext.Tables.Add(ToRow(table));
        dbContext.Fields.AddRange(table.Fields.Select(field => ToRow(table.Id, field)));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<Table?> GetAsync(
        Guid tenantId,
        Guid tableId,
        CancellationToken cancellationToken)
    {
        var row = await dbContext.Tables
            .AsNoTracking()
            .SingleOrDefaultAsync(
                table =>
                    table.TenantId == tenantId &&
                    table.Id == tableId &&
                    table.DeletedAt == null,
                cancellationToken);

        if (row is null)
        {
            return null;
        }

        var fieldRows = await dbContext.Fields
            .AsNoTracking()
            .Where(field => field.TableId == tableId)
            .OrderBy(field => field.Position)
            .ToListAsync(cancellationToken);

        return Table.Rehydrate(
            row.Id,
            row.TenantId,
            row.Name,
            row.SchemaVersion,
            row.Version,
            row.CreatedAt,
            row.UpdatedAt,
            row.DeletedAt,
            fieldRows.Select(ToDomain).ToArray());
    }

    public async Task<IReadOnlyList<Table>> ListAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.Tables
            .AsNoTracking()
            .Where(table => table.TenantId == tenantId && table.DeletedAt == null)
            .OrderBy(table => table.Name)
            .ToListAsync(cancellationToken);

        var tableIds = rows.Select(table => table.Id).ToArray();
        var fields = await dbContext.Fields
            .AsNoTracking()
            .Where(field => tableIds.Contains(field.TableId))
            .OrderBy(field => field.Position)
            .ToListAsync(cancellationToken);
        var fieldsByTable = fields.ToLookup(field => field.TableId);

        return rows.Select(row => Table.Rehydrate(
                row.Id,
                row.TenantId,
                row.Name,
                row.SchemaVersion,
                row.Version,
                row.CreatedAt,
                row.UpdatedAt,
                row.DeletedAt,
                fieldsByTable[row.Id].Select(ToDomain).ToArray()))
            .ToArray();
    }

    public async Task UpdateAsync(
        Table table,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            cancellationToken);
        var expectedVersion = table.Version - 1;
        var affected = await dbContext.Tables
            .Where(stored =>
                stored.Id == table.Id &&
                stored.TenantId == table.TenantId &&
                stored.Version == expectedVersion)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(stored => stored.Name, table.Name)
                    .SetProperty(stored => stored.SchemaVersion, table.SchemaVersion)
                    .SetProperty(stored => stored.Version, table.Version)
                    .SetProperty(stored => stored.UpdatedAt, table.UpdatedAt)
                    .SetProperty(stored => stored.DeletedAt, table.DeletedAt),
                cancellationToken);

        if (affected == 0)
        {
            throw new ConflictException(
                "table_version_conflict",
                "The table was modified by another request.");
        }

        var storedFields = await dbContext.Fields
            .Where(field => field.TableId == table.Id)
            .ToListAsync(cancellationToken);

        foreach (var field in table.Fields)
        {
            var storedField = storedFields.SingleOrDefault(row => row.Id == field.Id);

            if (storedField is null)
            {
                dbContext.Fields.Add(ToRow(table.Id, field));
                continue;
            }

            storedField.Label = field.Label;
            storedField.Required = field.Required;
            storedField.MinLength = field.MinLength;
            storedField.MaxLength = field.MaxLength;
            storedField.TextFormat = field.TextFormat;
            storedField.NumberMode = field.NumberMode;
            storedField.NumberMin = field.NumberMin;
            storedField.NumberMax = field.NumberMax;
            storedField.SelectOptions = JsonSerializer.SerializeToElement(field.SelectOptions);
            storedField.Position = field.Position;
            storedField.IsActive = field.IsActive;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static TableRow ToRow(Table table) =>
        new()
        {
            Id = table.Id,
            TenantId = table.TenantId,
            Name = table.Name,
            SchemaVersion = table.SchemaVersion,
            Version = table.Version,
            CreatedAt = table.CreatedAt,
            UpdatedAt = table.UpdatedAt,
            DeletedAt = table.DeletedAt,
        };

    private static FieldRow ToRow(Guid tableId, FieldDefinition field) =>
        new()
        {
            Id = field.Id,
            TableId = tableId,
            FieldKey = field.FieldKey,
            Label = field.Label,
            Type = field.Type,
            Required = field.Required,
            MinLength = field.MinLength,
            MaxLength = field.MaxLength,
            TextFormat = field.TextFormat,
            NumberMode = field.NumberMode,
            NumberMin = field.NumberMin,
            NumberMax = field.NumberMax,
            SelectOptions = JsonSerializer.SerializeToElement(field.SelectOptions),
            Position = field.Position,
            IsActive = field.IsActive,
        };

    private static FieldDefinition ToDomain(FieldRow field) =>
        field.Type switch
        {
            FieldType.Text => FieldDefinition.CreateText(
                field.Id,
                field.FieldKey,
                field.Label,
                field.Required,
                field.MinLength,
                field.MaxLength,
                field.TextFormat ?? TextFormat.Plain,
                field.Position,
                field.IsActive),
            FieldType.Number => FieldDefinition.CreateNumber(
                field.Id,
                field.FieldKey,
                field.Label,
                field.Required,
                field.NumberMode ?? NumberMode.Decimal,
                field.NumberMin,
                field.NumberMax,
                field.Position,
                field.IsActive),
            FieldType.Date => FieldDefinition.CreateDate(
                field.Id,
                field.FieldKey,
                field.Label,
                field.Required,
                field.Position,
                field.IsActive),
            FieldType.SingleSelect => FieldDefinition.CreateSingleSelect(
                field.Id,
                field.FieldKey,
                field.Label,
                field.Required,
                field.SelectOptions.Deserialize<SelectOption[]>() ?? [],
                field.Position,
                field.IsActive),
            _ => throw new InvalidOperationException(
                $"Unsupported field type {field.Type}."),
        };
}
