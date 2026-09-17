using System.Text.Json;
using TableMint.Domain.Tables;

namespace TableMint.Application.Tables;

public sealed class AddFieldRequest
{
    private AddFieldRequest(
        string fieldKey,
        string label,
        FieldType type,
        bool required,
        int expectedVersion,
        int? minLength,
        int? maxLength,
        TextFormat textFormat,
        NumberMode? numberMode,
        decimal? numberMin,
        decimal? numberMax,
        IReadOnlyList<SelectOption> selectOptions,
        JsonElement? defaultValue)
    {
        FieldKey = fieldKey;
        Label = label;
        Type = type;
        Required = required;
        ExpectedVersion = expectedVersion;
        MinLength = minLength;
        MaxLength = maxLength;
        TextFormat = textFormat;
        NumberMode = numberMode;
        NumberMin = numberMin;
        NumberMax = numberMax;
        SelectOptions = selectOptions;
        DefaultValue = defaultValue?.Clone();
    }

    public string FieldKey { get; }

    public string Label { get; }

    public FieldType Type { get; }

    public bool Required { get; }

    public int ExpectedVersion { get; }

    public int? MinLength { get; }

    public int? MaxLength { get; }

    public TextFormat TextFormat { get; }

    public NumberMode? NumberMode { get; }

    public decimal? NumberMin { get; }

    public decimal? NumberMax { get; }

    public IReadOnlyList<SelectOption> SelectOptions { get; }

    public JsonElement? DefaultValue { get; }

    public static AddFieldRequest Text(
        string fieldKey,
        string label,
        bool required,
        int expectedVersion,
        int? minLength = null,
        int? maxLength = null,
        TextFormat format = global::TableMint.Domain.Tables.TextFormat.Plain,
        JsonElement? defaultValue = null) =>
        new(
            fieldKey,
            label,
            FieldType.Text,
            required,
            expectedVersion,
            minLength,
            maxLength,
            format,
            numberMode: null,
            numberMin: null,
            numberMax: null,
            selectOptions: [],
            defaultValue);

    public static AddFieldRequest Number(
        string fieldKey,
        string label,
        bool required,
        int expectedVersion,
        NumberMode mode,
        decimal? min = null,
        decimal? max = null,
        JsonElement? defaultValue = null) =>
        new(
            fieldKey,
            label,
            FieldType.Number,
            required,
            expectedVersion,
            minLength: null,
            maxLength: null,
            textFormat: global::TableMint.Domain.Tables.TextFormat.Plain,
            numberMode: mode,
            numberMin: min,
            numberMax: max,
            selectOptions: [],
            defaultValue);

    public static AddFieldRequest Date(
        string fieldKey,
        string label,
        bool required,
        int expectedVersion,
        JsonElement? defaultValue = null) =>
        new(
            fieldKey,
            label,
            FieldType.Date,
            required,
            expectedVersion,
            minLength: null,
            maxLength: null,
            textFormat: global::TableMint.Domain.Tables.TextFormat.Plain,
            numberMode: null,
            numberMin: null,
            numberMax: null,
            selectOptions: [],
            defaultValue);

    public static AddFieldRequest SingleSelect(
        string fieldKey,
        string label,
        bool required,
        int expectedVersion,
        IReadOnlyList<SelectOption> options,
        JsonElement? defaultValue = null) =>
        new(
            fieldKey,
            label,
            FieldType.SingleSelect,
            required,
            expectedVersion,
            minLength: null,
            maxLength: null,
            textFormat: global::TableMint.Domain.Tables.TextFormat.Plain,
            numberMode: null,
            numberMin: null,
            numberMax: null,
            selectOptions: options,
            defaultValue);
}
