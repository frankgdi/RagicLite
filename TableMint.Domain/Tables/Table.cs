namespace TableMint.Domain.Tables;

public sealed class Table
{
    private readonly List<FieldDefinition> _fields = [];

    private Table(
        Guid id,
        Guid tenantId,
        string name,
        int schemaVersion,
        int version,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        DateTimeOffset? deletedAt,
        IReadOnlyList<FieldDefinition> fields)
    {
        Id = id;
        TenantId = tenantId;
        Name = name;
        SchemaVersion = schemaVersion;
        Version = version;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        DeletedAt = deletedAt;
        _fields.AddRange(fields);
    }

    public Guid Id { get; }

    public Guid TenantId { get; }

    public string Name { get; private set; }

    public int SchemaVersion { get; private set; }

    public int Version { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public IReadOnlyList<FieldDefinition> Fields => _fields;

    public static Table Create(
        Guid id,
        Guid tenantId,
        string name,
        DateTimeOffset createdAt) =>
        new(
            id,
            tenantId,
            name,
            schemaVersion: 1,
            version: 1,
            createdAt,
            updatedAt: createdAt,
            deletedAt: null,
            fields: []);

    public static Table Rehydrate(
        Guid id,
        Guid tenantId,
        string name,
        int schemaVersion,
        int version,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        DateTimeOffset? deletedAt,
        IReadOnlyList<FieldDefinition> fields) =>
        new(
            id,
            tenantId,
            name,
            schemaVersion,
            version,
            createdAt,
            updatedAt,
            deletedAt,
            fields);

    public void AddField(FieldDefinition field, DateTimeOffset updatedAt)
    {
        _fields.Add(field);
        SchemaVersion++;
        Version++;
        UpdatedAt = updatedAt;
    }

    public bool UpdateField(
        Guid fieldId,
        string? label,
        bool? isActive,
        bool? required,
        TextFormat? format,
        int? minLength,
        int? maxLength,
        decimal? min,
        decimal? max,
        IReadOnlyList<SelectOption>? options,
        DateTimeOffset updatedAt)
    {
        var field = _fields.SingleOrDefault(candidate => candidate.Id == fieldId);
        if (field is null)
        {
            return false;
        }

        if (label is not null)
        {
            field.Rename(label);
        }

        var schemaChanged = false;
        if (isActive is not null && field.IsActive != isActive.Value)
        {
            field.SetActive(isActive.Value);
            schemaChanged = true;
        }

        if (required is not null && field.Required != required.Value)
        {
            field.SetRequired(required.Value);
            schemaChanged = true;
        }

        if (format is not null && field.TextFormat != format.Value)
        {
            field.SetTextFormat(format.Value);
            schemaChanged = true;
        }

        if (minLength is not null || maxLength is not null)
        {
            field.UpdateTextRules(
                minLength ?? field.MinLength,
                maxLength ?? field.MaxLength);
            schemaChanged = true;
        }

        if (min is not null || max is not null)
        {
            field.UpdateNumberRules(
                min ?? field.NumberMin,
                max ?? field.NumberMax);
            schemaChanged = true;
        }

        if (options is not null)
        {
            field.UpdateSelectOptions(options);
            schemaChanged = true;
        }

        if (schemaChanged)
        {
            SchemaVersion++;
        }

        Version++;
        UpdatedAt = updatedAt;
        return true;
    }

    public void ReorderFields(
        IReadOnlyList<Guid> fieldIds,
        DateTimeOffset updatedAt)
    {
        var fieldsById = _fields.ToDictionary(field => field.Id);
        for (var position = 0; position < fieldIds.Count; position++)
        {
            fieldsById[fieldIds[position]].MoveTo(position);
        }

        _fields.Sort((left, right) => left.Position.CompareTo(right.Position));
        Version++;
        UpdatedAt = updatedAt;
    }

    public void Rename(string name, DateTimeOffset updatedAt)
    {
        Name = name;
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
