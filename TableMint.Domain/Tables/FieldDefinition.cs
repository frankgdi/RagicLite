namespace TableMint.Domain.Tables;

public sealed class FieldDefinition
{
    private FieldDefinition(
        Guid id,
        string fieldKey,
        string label,
        bool required,
        FieldType type,
        int? minLength,
        int? maxLength,
        TextFormat? textFormat,
        NumberMode? numberMode,
        decimal? numberMin,
        decimal? numberMax,
        IReadOnlyList<SelectOption> selectOptions,
        int position,
        bool isActive)
    {
        Id = id;
        FieldKey = fieldKey;
        Label = label;
        Required = required;
        Type = type;
        MinLength = minLength;
        MaxLength = maxLength;
        TextFormat = textFormat;
        NumberMode = numberMode;
        NumberMin = numberMin;
        NumberMax = numberMax;
        SelectOptions = selectOptions;
        Position = position;
        IsActive = isActive;
    }

    public Guid Id { get; }

    public string FieldKey { get; }

    public string Label { get; private set; }

    public bool Required { get; private set; }

    public FieldType Type { get; }

    public int? MinLength { get; private set; }

    public int? MaxLength { get; private set; }

    public TextFormat? TextFormat { get; private set; }

    public NumberMode? NumberMode { get; }

    public decimal? NumberMin { get; private set; }

    public decimal? NumberMax { get; private set; }

    public IReadOnlyList<SelectOption> SelectOptions { get; private set; }

    public int Position { get; private set; }

    public bool IsActive { get; private set; }

    public static FieldDefinition CreateText(
        Guid id,
        string fieldKey,
        string label,
        bool required,
        int? minLength = null,
        int? maxLength = null,
        TextFormat format = global::TableMint.Domain.Tables.TextFormat.Plain,
        int position = 0,
        bool isActive = true) =>
        new(
            id,
            fieldKey,
            label,
            required,
            FieldType.Text,
            minLength,
            maxLength,
            format,
            numberMode: null,
            numberMin: null,
            numberMax: null,
            selectOptions: [],
            position,
            isActive);

    public static FieldDefinition CreateNumber(
        Guid id,
        string fieldKey,
        string label,
        bool required,
        NumberMode mode,
        decimal? min = null,
        decimal? max = null,
        int position = 0,
        bool isActive = true) =>
        new(
            id,
            fieldKey,
            label,
            required,
            FieldType.Number,
            minLength: null,
            maxLength: null,
            textFormat: null,
            numberMode: mode,
            numberMin: min,
            numberMax: max,
            selectOptions: [],
            position,
            isActive);

    public static FieldDefinition CreateDate(
        Guid id,
        string fieldKey,
        string label,
        bool required,
        int position = 0,
        bool isActive = true) =>
        new(
            id,
            fieldKey,
            label,
            required,
            FieldType.Date,
            minLength: null,
            maxLength: null,
            textFormat: null,
            numberMode: null,
            numberMin: null,
            numberMax: null,
            selectOptions: [],
            position,
            isActive);

    public static FieldDefinition CreateSingleSelect(
        Guid id,
        string fieldKey,
        string label,
        bool required,
        IReadOnlyList<SelectOption> options,
        int position = 0,
        bool isActive = true) =>
        new(
            id,
            fieldKey,
            label,
            required,
            FieldType.SingleSelect,
            minLength: null,
            maxLength: null,
            textFormat: null,
            numberMode: null,
            numberMin: null,
            numberMax: null,
            selectOptions: options,
            position,
            isActive);

    public void Rename(string label) => Label = label;

    public void MoveTo(int position) => Position = position;

    public void SetActive(bool isActive) => IsActive = isActive;

    public void UpdateTextRules(int? minLength, int? maxLength)
    {
        MinLength = minLength;
        MaxLength = maxLength;
    }

    public void UpdateNumberRules(decimal? min, decimal? max)
    {
        NumberMin = min;
        NumberMax = max;
    }

    public void UpdateSelectOptions(IReadOnlyList<SelectOption> options) =>
        SelectOptions = options;

    public void SetRequired(bool required) => Required = required;

    public void SetTextFormat(TextFormat format) => TextFormat = format;
}
