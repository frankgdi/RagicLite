using TableMint.Domain.Tables;

namespace TableMint.Application.Tables;

public sealed record UpdateFieldRequest(
    string? Label,
    int ExpectedVersion,
    bool? IsActive = null,
    int? MinLength = null,
    int? MaxLength = null,
    decimal? Min = null,
    decimal? Max = null,
    IReadOnlyList<SelectOption>? Options = null,
    bool? Required = null,
    TextFormat? Format = null);
