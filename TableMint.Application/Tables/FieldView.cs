using TableMint.Domain.Tables;

namespace TableMint.Application.Tables;

public sealed record FieldView(
    Guid Id,
    string FieldKey,
    string Label,
    FieldType Type,
    bool Required,
    int Position,
    bool IsActive,
    int? MinLength,
    int? MaxLength,
    TextFormat? TextFormat,
    NumberMode? NumberMode,
    decimal? Min,
    decimal? Max,
    IReadOnlyList<SelectOption> Options);
