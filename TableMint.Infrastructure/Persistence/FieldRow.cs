using System.Text.Json;
using TableMint.Domain.Tables;

namespace TableMint.Infrastructure.Persistence;

internal sealed class FieldRow
{
    public Guid Id { get; set; }

    public Guid TableId { get; set; }

    public required string FieldKey { get; set; }

    public required string Label { get; set; }

    public FieldType Type { get; set; }

    public bool Required { get; set; }

    public int? MinLength { get; set; }

    public int? MaxLength { get; set; }

    public TextFormat? TextFormat { get; set; }

    public NumberMode? NumberMode { get; set; }

    public decimal? NumberMin { get; set; }

    public decimal? NumberMax { get; set; }

    public JsonElement SelectOptions { get; set; }

    public int Position { get; set; }

    public bool IsActive { get; set; }
}
