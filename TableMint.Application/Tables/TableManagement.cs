using System.Text.Json;
using TableMint.Application.Abstractions;
using TableMint.Application.Errors;
using TableMint.Application.Validation;
using TableMint.Domain.Tables;

namespace TableMint.Application.Tables;

public sealed class TableManagement
{
    private readonly ITableRepository tables;
    private readonly IRecordRepository? records;
    private readonly ITenantContext tenantContext;
    private readonly IClock clock;

    public TableManagement(
        ITableRepository tables,
        ITenantContext tenantContext,
        IClock clock)
        : this(tables, null, tenantContext, clock)
    {
    }

    public TableManagement(
        ITableRepository tables,
        IRecordRepository? records,
        ITenantContext tenantContext,
        IClock clock)
    {
        this.tables = tables;
        this.records = records;
        this.tenantContext = tenantContext;
        this.clock = clock;
    }
    public async Task<TableView> CreateAsync(
        CreateTableRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new RequestValidationException(
                "table_name_required",
                "Table name is required.");
        }

        var table = Table.Create(
            Guid.NewGuid(),
            tenantContext.TenantId,
            request.Name,
            clock.UtcNow);

        foreach (var fieldRequest in request.Fields ?? [])
        {
            ValidateAddFieldRequest(fieldRequest);

            if (table.Fields.Any(field => field.FieldKey == fieldRequest.FieldKey))
            {
                throw new RequestValidationException(
                    "duplicate_field_key",
                    "Field key must be unique within a table.");
            }

            table.AddField(
                CreateFieldDefinition(fieldRequest, table.Fields.Count),
                clock.UtcNow);
        }

        await tables.AddAsync(table, cancellationToken);

        return ToView(table);
    }

    public async Task<TableView?> GetAsync(
        Guid tableId,
        CancellationToken cancellationToken = default)
    {
        var table = await tables.GetAsync(
            tenantContext.TenantId,
            tableId,
            cancellationToken);

        return table is null || table.DeletedAt is not null ? null : ToView(table);
    }

    public async Task<IReadOnlyList<TableView>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        var tenantTables = await tables.ListAsync(
            tenantContext.TenantId,
            cancellationToken);

        return tenantTables.Select(ToView).ToArray();
    }

    public async Task<TableView> UpdateAsync(
        Guid tableId,
        UpdateTableRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new RequestValidationException(
                "table_name_required",
                "Table name is required.");
        }

        var table = await GetTableOrThrowAsync(tableId, cancellationToken);
        EnsureCurrentVersion(table, request.ExpectedVersion);

        table.Rename(request.Name, clock.UtcNow);
        await tables.UpdateAsync(table, cancellationToken);

        return ToView(table);
    }

    public async Task DeleteAsync(
        Guid tableId,
        int expectedVersion,
        CancellationToken cancellationToken = default)
    {
        var table = await GetTableOrThrowAsync(tableId, cancellationToken);
        EnsureCurrentVersion(table, expectedVersion);

        table.Delete(clock.UtcNow);
        await tables.UpdateAsync(table, cancellationToken);
    }

    public async Task<TableView> AddFieldAsync(
        Guid tableId,
        AddFieldRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateAddFieldRequest(request);

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

        if (table.Version != request.ExpectedVersion)
        {
            throw new ConflictException(
                "table_version_conflict",
                "The table was modified by another request.");
        }

        if (table.Fields.Any(field =>
                string.Equals(
                    field.FieldKey,
                    request.FieldKey,
                    StringComparison.Ordinal)))
        {
            throw new RequestValidationException(
                "duplicate_field_key",
                "Field key must be unique within a table.");
        }

        var field = CreateFieldDefinition(request, table.Fields.Count);

        IReadOnlyList<Record> existingRecords = records is null
            ? []
            : await records.ListAsync(
                tenantContext.TenantId,
                tableId,
                cancellationToken);
        if (existingRecords.Count > 0 && request.Required && request.DefaultValue is null)
        {
            throw new RequestValidationException(
                "default_value_required",
                "A default value is required when adding a required field to existing records.");
        }

        var recordsToBackfill = new List<(Record Record, JsonElement Data)>();
        if (request.DefaultValue is not null)
        {
            var proposedSchema = new TableSchema(
                table.SchemaVersion + 1,
                [.. table.Fields, field]);
            var dynamicValidator = new DynamicValidator();

            if (existingRecords.Count == 0)
            {
                var sample = AddValue(
                    JsonSerializer.SerializeToElement(new Dictionary<string, JsonElement>()),
                    field.FieldKey,
                    request.DefaultValue.Value);
                if (!dynamicValidator.Validate(
                        new TableSchema(proposedSchema.Version, [field]),
                        sample).IsValid)
                {
                    throw new RequestValidationException(
                        "invalid_default_value",
                        "Default value does not satisfy the field rules.");
                }
            }

            foreach (var record in existingRecords)
            {
                var data = AddValue(
                    record.Data,
                    field.FieldKey,
                    request.DefaultValue.Value);
                if (!dynamicValidator.Validate(
                        proposedSchema,
                        data,
                        record.Data).IsValid)
                {
                    throw new RequestValidationException(
                        "invalid_default_value",
                        "Default value does not satisfy the table schema.");
                }

                recordsToBackfill.Add((record, data));
            }
        }

        table.AddField(field, clock.UtcNow);
        await tables.UpdateAsync(table, cancellationToken);

        foreach (var (record, data) in recordsToBackfill)
        {
            record.Update(data, table.SchemaVersion, clock.UtcNow);
            await records!.UpdateAsync(record, cancellationToken);
        }

        return ToView(table);
    }

    private static FieldDefinition CreateFieldDefinition(
        AddFieldRequest request,
        int position)
    {
        var fieldId = Guid.NewGuid();
        return request.Type switch
        {
            FieldType.Text => FieldDefinition.CreateText(
                fieldId,
                request.FieldKey,
                request.Label,
                request.Required,
                request.MinLength,
                request.MaxLength,
                request.TextFormat,
                position),
            FieldType.Number => FieldDefinition.CreateNumber(
                fieldId,
                request.FieldKey,
                request.Label,
                request.Required,
                request.NumberMode ?? NumberMode.Decimal,
                request.NumberMin,
                request.NumberMax,
                position),
            FieldType.Date => FieldDefinition.CreateDate(
                fieldId,
                request.FieldKey,
                request.Label,
                request.Required,
                position),
            FieldType.SingleSelect => FieldDefinition.CreateSingleSelect(
                fieldId,
                request.FieldKey,
                request.Label,
                request.Required,
                request.SelectOptions,
                position),
            _ => throw new RequestValidationException(
                "unsupported_field_type",
                "The field type is not supported."),
        };
    }

    private static void ValidateAddFieldRequest(AddFieldRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FieldKey))
        {
            throw new RequestValidationException(
                "field_key_required",
                "Field key is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Label))
        {
            throw new RequestValidationException(
                "field_label_required",
                "Field label is required.");
        }

        if (request.Type is FieldType.Text &&
            (request.MinLength is < 0 || request.MaxLength is < 0 ||
             request.MinLength is not null && request.MaxLength is not null &&
             request.MinLength > request.MaxLength))
        {
            throw new RequestValidationException(
                "invalid_field_rules",
                "Text length rules must be non-negative and minimum cannot exceed maximum.");
        }

        if (request.Type is FieldType.Number &&
            request.NumberMin is not null && request.NumberMax is not null &&
            request.NumberMin > request.NumberMax)
        {
            throw new RequestValidationException(
                "invalid_field_rules",
                "Number minimum cannot exceed maximum.");
        }

        if (request.Type is FieldType.SingleSelect &&
            (request.SelectOptions.Count == 0 ||
             request.SelectOptions.Any(option =>
                 string.IsNullOrWhiteSpace(option.Value) ||
                 string.IsNullOrWhiteSpace(option.Label)) ||
             request.SelectOptions.Select(option => option.Value)
                 .Distinct(StringComparer.Ordinal).Count() != request.SelectOptions.Count))
        {
            throw new RequestValidationException(
                "invalid_select_options",
                "Select options must have unique, non-empty values and labels.");
        }
    }

    private static JsonElement AddValue(
        JsonElement data,
        string fieldKey,
        JsonElement value)
    {
        var values = data.EnumerateObject().ToDictionary(
            property => property.Name,
            property => property.Value.Clone(),
            StringComparer.Ordinal);
        values[fieldKey] = value.Clone();
        return JsonSerializer.SerializeToElement(values);
    }

    public async Task<TableView> UpdateFieldAsync(
        Guid tableId,
        Guid fieldId,
        UpdateFieldRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Label is not null && string.IsNullOrWhiteSpace(request.Label))
        {
            throw new RequestValidationException(
                "field_label_required",
                "Field label is required.");
        }

        var hasRuleChanges = request.MinLength is not null ||
            request.MaxLength is not null ||
            request.Min is not null ||
            request.Max is not null ||
            request.Options is not null ||
            request.Required is not null ||
            request.Format is not null;
        if (request.Label is null && request.IsActive is null && !hasRuleChanges)
        {
            throw new RequestValidationException(
                "field_update_required",
                "At least one field change is required.");
        }

        var table = await GetTableOrThrowAsync(tableId, cancellationToken);
        EnsureCurrentVersion(table, request.ExpectedVersion);

        var field = table.Fields.SingleOrDefault(candidate => candidate.Id == fieldId);
        if (field is null)
        {
            throw new NotFoundException("field_not_found", "Field was not found.");
        }

        ValidateRuleRequest(field, request);
        if (hasRuleChanges)
        {
            var recordRepository = records ?? throw new InvalidOperationException(
                "Record repository is required for schema rule changes.");
            var proposedField = CreateProposedField(field, request);
            var proposedFields = table.Fields
                .Select(candidate => candidate.Id == fieldId ? proposedField : candidate)
                .ToArray();
            var existingRecords = await recordRepository.ListAsync(
                tenantContext.TenantId,
                tableId,
                cancellationToken);
            var validator = new DynamicValidator();
            var affectedCount = existingRecords.Count(record =>
                !validator.Validate(
                    new TableSchema(table.SchemaVersion + 1, proposedFields),
                    record.Data,
                    record.Data).IsValid);

            if (affectedCount > 0)
            {
                throw new SchemaChangeRejectedException(affectedCount);
            }
        }

        if (!table.UpdateField(
                fieldId,
                request.Label,
                request.IsActive,
                request.Required,
                request.Format,
                request.MinLength,
                request.MaxLength,
                request.Min,
                request.Max,
                request.Options,
                clock.UtcNow))
        {
            throw new NotFoundException(
                "field_not_found",
                "Field was not found.");
        }

        await tables.UpdateAsync(table, cancellationToken);
        return ToView(table);
    }

    private static void ValidateRuleRequest(
        FieldDefinition field,
        UpdateFieldRequest request)
    {
        if ((request.MinLength is not null || request.MaxLength is not null) &&
            field.Type is not FieldType.Text)
        {
            throw new RequestValidationException(
                "invalid_field_rules",
                "Text length rules can only be applied to text fields.");
        }

        if (field.Type is FieldType.Text)
        {
            var minLength = request.MinLength ?? field.MinLength;
            var maxLength = request.MaxLength ?? field.MaxLength;
            if (minLength is < 0 || maxLength is < 0 ||
                minLength is not null && maxLength is not null && minLength > maxLength)
            {
                throw new RequestValidationException(
                    "invalid_field_rules",
                    "Text length rules must be non-negative and minimum cannot exceed maximum.");
            }
        }

        if ((request.Min is not null || request.Max is not null) &&
            field.Type is not FieldType.Number)
        {
            throw new RequestValidationException(
                "invalid_field_rules",
                "Number range rules can only be applied to number fields.");
        }

        if (field.Type is FieldType.Number)
        {
            var min = request.Min ?? field.NumberMin;
            var max = request.Max ?? field.NumberMax;
            if (min is not null && max is not null && min > max)
            {
                throw new RequestValidationException(
                    "invalid_field_rules",
                    "Number minimum cannot exceed maximum.");
            }
        }


        if (request.Options is not null)
        {
            if (field.Type is not FieldType.SingleSelect)
            {
                throw new RequestValidationException(
                    "invalid_field_rules",
                    "Options can only be applied to single-select fields.");
            }

            if (request.Options.Count == 0 ||
                request.Options.Any(option =>
                    string.IsNullOrWhiteSpace(option.Value) ||
                    string.IsNullOrWhiteSpace(option.Label)) ||
                request.Options.Select(option => option.Value)
                    .Distinct(StringComparer.Ordinal).Count() !=
                    request.Options.Count)
            {
                throw new RequestValidationException(
                    "invalid_select_options",
                    "Select options must have unique, non-empty values.");
            }
        }

        if (request.Format is not null && field.Type is not FieldType.Text)
        {
            throw new RequestValidationException(
                "invalid_field_rules",
                "Text format can only be applied to text fields.");
        }
    }

    private static FieldDefinition CreateProposedField(
        FieldDefinition field,
        UpdateFieldRequest request) =>
        field.Type switch
        {
            FieldType.Text => FieldDefinition.CreateText(
                field.Id,
                field.FieldKey,
                request.Label ?? field.Label,
                request.Required ?? field.Required,
                request.MinLength ?? field.MinLength,
                request.MaxLength ?? field.MaxLength,
                request.Format ?? field.TextFormat ?? TextFormat.Plain,
                field.Position,
                request.IsActive ?? field.IsActive),
            FieldType.Number => FieldDefinition.CreateNumber(
                field.Id,
                field.FieldKey,
                request.Label ?? field.Label,
                request.Required ?? field.Required,
                field.NumberMode ?? NumberMode.Decimal,
                request.Min ?? field.NumberMin,
                request.Max ?? field.NumberMax,
                field.Position,
                request.IsActive ?? field.IsActive),
            FieldType.Date => FieldDefinition.CreateDate(
                field.Id,
                field.FieldKey,
                request.Label ?? field.Label,
                request.Required ?? field.Required,
                field.Position,
                request.IsActive ?? field.IsActive),
            FieldType.SingleSelect => FieldDefinition.CreateSingleSelect(
                field.Id,
                field.FieldKey,
                request.Label ?? field.Label,
                request.Required ?? field.Required,
                request.Options ?? field.SelectOptions,
                field.Position,
                request.IsActive ?? field.IsActive),
            _ => field,
        };

    public async Task<TableView> ReorderFieldsAsync(
        Guid tableId,
        ReorderFieldsRequest request,
        CancellationToken cancellationToken = default)
    {
        var table = await GetTableOrThrowAsync(tableId, cancellationToken);
        EnsureCurrentVersion(table, request.ExpectedVersion);

        var currentIds = table.Fields.Select(field => field.Id).ToHashSet();
        if (request.FieldIds.Count != currentIds.Count ||
            request.FieldIds.Distinct().Count() != request.FieldIds.Count ||
            request.FieldIds.Any(fieldId => !currentIds.Contains(fieldId)))
        {
            throw new RequestValidationException(
                "field_order_must_be_complete",
                "Field order must contain every field exactly once.");
        }

        table.ReorderFields(request.FieldIds, clock.UtcNow);
        await tables.UpdateAsync(table, cancellationToken);
        return ToView(table);
    }

    private async Task<Table> GetTableOrThrowAsync(
        Guid tableId,
        CancellationToken cancellationToken)
    {
        var table = await tables.GetAsync(
            tenantContext.TenantId,
            tableId,
            cancellationToken);

        if (table is null || table.DeletedAt is not null)
        {
            throw new NotFoundException(
                "table_not_found",
                "Table was not found.");
        }

        return table;
    }

    private static void EnsureCurrentVersion(Table table, int expectedVersion)
    {
        if (table.Version != expectedVersion)
        {
            throw new ConflictException(
                "table_version_conflict",
                "The table was modified by another request.");
        }
    }

    private static TableView ToView(Table table) =>
        new(
            table.Id,
            table.TenantId,
            table.Name,
            table.SchemaVersion,
            table.Version,
            table.CreatedAt,
            table.UpdatedAt,
            table.Fields
                .OrderBy(field => field.Position)
                .Select(field => new FieldView(
                    field.Id,
                    field.FieldKey,
                    field.Label,
                    field.Type,
                    field.Required,
                    field.Position,
                    field.IsActive,
                    field.MinLength,
                    field.MaxLength,
                    field.TextFormat,
                    field.NumberMode,
                    field.NumberMin,
                    field.NumberMax,
                    field.SelectOptions))
                .ToArray());
}
