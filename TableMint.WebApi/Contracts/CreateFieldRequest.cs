using System.Text.Json;
using TableMint.Domain.Tables;

namespace TableMint.WebApi.Contracts;

public sealed record CreateFieldRequest(
    string FieldKey,
    string Label,
    FieldType Type,
    bool Required,
    int ExpectedVersion,
    int? MinLength = null,
    int? MaxLength = null,
    TextFormat Format = TextFormat.Plain,
    NumberMode? NumberMode = null,
    decimal? Min = null,
    decimal? Max = null,
    IReadOnlyList<SelectOption>? Options = null,
    JsonElement? DefaultValue = null);
