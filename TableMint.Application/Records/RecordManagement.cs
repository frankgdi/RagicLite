using System.Text.Json;
using System.Globalization;
using TableMint.Application.Abstractions;
using TableMint.Application.Errors;
using TableMint.Application.Validation;
using TableMint.Domain.Tables;

namespace TableMint.Application.Records;

public sealed class RecordManagement(
    IRecordRepository records,
    ITableRepository tables,
    ITenantContext tenantContext,
    IClock clock,
    DynamicValidator validator)
{
    public async Task<RecordView> CreateAsync(
        Guid tableId,
        CreateRecordRequest request,
        CancellationToken cancellationToken = default)
    {
        var table = await tables.GetAsync(
            tenantContext.TenantId,
            tableId,
            cancellationToken);

        if (table is null)
        {
            throw new NotFoundException(
                "table_not_found",
                "Table was not found.");
        }

        var result = validator.Validate(
            new TableSchema(table.SchemaVersion, table.Fields),
            request.Data);

        if (!result.IsValid)
        {
            throw new RecordValidationException(result.Errors);
        }

        var record = Record.Create(
            Guid.NewGuid(),
            tenantContext.TenantId,
            table.Id,
            request.Data,
            table.SchemaVersion,
            clock.UtcNow);

        await records.AddAsync(record, cancellationToken);

        return ToView(record);
    }

    public async Task<RecordView?> GetAsync(
        Guid tableId,
        Guid recordId,
        CancellationToken cancellationToken = default)
    {
        var record = await records.GetAsync(
            tenantContext.TenantId,
            tableId,
            recordId,
            cancellationToken);

        return record is null || record.DeletedAt is not null
            ? null
            : ToView(record);
    }

    public async Task<RecordPageView> ListAsync(
        Guid tableId,
        RecordListRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Page < 1 || request.PageSize is < 1 or > 100)
        {
            throw new RequestValidationException(
                "invalid_pagination",
                "Page must be at least 1 and page size must be between 1 and 100.");
        }

        var table = await tables.GetAsync(
            tenantContext.TenantId,
            tableId,
            cancellationToken);
        if (table is null)
        {
            throw new NotFoundException("table_not_found", "Table was not found.");
        }

        if ((request.FilterField is null) != (request.FilterValue is null))
        {
            throw new RequestValidationException(
                "incomplete_filter",
                "Filter field and filter value must be provided together.");
        }

        string? filterFieldKey = null;
        JsonElement? filterValue = null;
        if (request.FilterField is not null)
        {
            var field = table.Fields.SingleOrDefault(candidate =>
                candidate.FieldKey == request.FilterField);
            if (field is null || !field.IsActive)
            {
                throw new RequestValidationException(
                    "invalid_filter_field",
                    "Filter field does not exist or is inactive.");
            }

            filterFieldKey = field.FieldKey;
            filterValue = ParseFilterValue(field, request.FilterValue!);
        }

        var page = await records.QueryAsync(
            tenantContext.TenantId,
            tableId,
            new RecordQuery(
                request.Page,
                request.PageSize,
                request.SortBy,
                request.SortDirection,
                filterFieldKey,
                filterValue),
            cancellationToken);

        return new RecordPageView(
            page.Items.Select(ToView).ToArray(),
            request.Page,
            request.PageSize,
            page.TotalCount);
    }

    private static JsonElement ParseFilterValue(
        FieldDefinition field,
        string value)
    {
        if (field.Type is FieldType.Number && field.NumberMode is NumberMode.Integer)
        {
            if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
            {
                return JsonSerializer.SerializeToElement(integer);
            }

            throw InvalidFilterValue(field);
        }

        if (field.Type is FieldType.Number)
        {
            if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
            {
                return JsonSerializer.SerializeToElement(number);
            }

            throw InvalidFilterValue(field);
        }

        if (field.Type is FieldType.Date &&
            !DateOnly.TryParseExact(
                value,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _))
        {
            throw InvalidFilterValue(field);
        }

        return JsonSerializer.SerializeToElement(value);
    }

    private static RequestValidationException InvalidFilterValue(FieldDefinition field) =>
        new(
            "invalid_filter_value",
            $"Filter value is not valid for {field.Label}.");

    public async Task DeleteAsync(
        Guid tableId,
        Guid recordId,
        int expectedVersion,
        CancellationToken cancellationToken = default)
    {
        var record = await records.GetAsync(
            tenantContext.TenantId,
            tableId,
            recordId,
            cancellationToken);

        if (record is null || record.DeletedAt is not null)
        {
            throw new NotFoundException(
                "record_not_found",
                "Record was not found.");
        }

        if (record.Version != expectedVersion)
        {
            throw new ConflictException(
                "record_version_conflict",
                "The record was modified by another request.");
        }

        record.Delete(clock.UtcNow);
        await records.UpdateAsync(record, cancellationToken);
    }

    public async Task<RecordView> UpdateAsync(
        Guid tableId,
        Guid recordId,
        UpdateRecordRequest request,
        CancellationToken cancellationToken = default)
    {
        var record = await records.GetAsync(
            tenantContext.TenantId,
            tableId,
            recordId,
            cancellationToken);

        if (record is null)
        {
            throw new NotFoundException(
                "record_not_found",
                "Record was not found.");
        }

        if (record.Version != request.ExpectedVersion)
        {
            throw new ConflictException(
                "record_version_conflict",
                "The record was modified by another request.");
        }

        var table = await tables.GetAsync(
            tenantContext.TenantId,
            tableId,
            cancellationToken);

        if (table is null)
        {
            throw new NotFoundException(
                "table_not_found",
                "Table was not found.");
        }

        if (request.Data.ValueKind is not JsonValueKind.Object)
        {
            var shapeResult = validator.Validate(
                new TableSchema(table.SchemaVersion, table.Fields),
                request.Data,
                record.Data);
            throw new RecordValidationException(shapeResult.Errors);
        }

        var mergedData = Merge(record.Data, request.Data);
        var result = validator.Validate(
            new TableSchema(table.SchemaVersion, table.Fields),
            mergedData,
            record.Data);

        if (!result.IsValid)
        {
            throw new RecordValidationException(result.Errors);
        }

        record.Update(mergedData, table.SchemaVersion, clock.UtcNow);
        await records.UpdateAsync(record, cancellationToken);

        return ToView(record);
    }

    private static JsonElement Merge(JsonElement current, JsonElement patch)
    {
        var values = current
            .EnumerateObject()
            .ToDictionary(
                property => property.Name,
                property => property.Value.Clone(),
                StringComparer.Ordinal);

        foreach (var property in patch.EnumerateObject())
        {
            values[property.Name] = property.Value.Clone();
        }

        return JsonSerializer.SerializeToElement(values);
    }

    private static RecordView ToView(Record record) =>
        new(
            record.Id,
            record.TenantId,
            record.TableId,
            record.Data.Clone(),
            record.SchemaVersion,
            record.Version,
            record.CreatedAt,
            record.UpdatedAt);
}
