using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json;
using TableMint.Domain.Tables;

namespace TableMint.Application.Validation;

public sealed class DynamicValidator
{
    public ValidationResult Validate(
        TableSchema schema,
        JsonElement recordData,
        JsonElement? existingData = null)
    {
        var errors = new List<ValidationError>();

        if (recordData.ValueKind is not JsonValueKind.Object)
        {
            errors.Add(new ValidationError(
                null,
                "$",
                "Record data",
                "invalid_record_data",
                "Record data must be a JSON object."));

            return new ValidationResult(errors);
        }

        foreach (var property in recordData.EnumerateObject())
        {
            if (schema.Fields.All(field => field.FieldKey != property.Name))
            {
                errors.Add(new ValidationError(
                    null,
                    property.Name,
                    property.Name,
                    "unknown_field",
                    $"{property.Name} is not defined in the table schema."));
            }
        }

        foreach (var field in schema.Fields)
        {
            var isMissing = !recordData.TryGetProperty(field.FieldKey, out var value);
            if (!field.IsActive)
            {
                var preservesExistingValue =
                    !isMissing &&
                    existingData is not null &&
                    existingData.Value.ValueKind is JsonValueKind.Object &&
                    existingData.Value.TryGetProperty(field.FieldKey, out var existingValue) &&
                    JsonElement.DeepEquals(value, existingValue);

                if (!isMissing && !preservesExistingValue)
                {
                    errors.Add(new ValidationError(
                        field.Id,
                        field.FieldKey,
                        field.Label,
                        "inactive_field",
                        $"{field.Label} is inactive and cannot accept a value."));
                }

                continue;
            }

            var isBlank = !isMissing &&
                (value.ValueKind is JsonValueKind.Null ||
                 value.ValueKind is JsonValueKind.String &&
                 string.IsNullOrWhiteSpace(value.GetString()));

            if (field.Required && (isMissing || isBlank))
            {
                errors.Add(new ValidationError(
                    field.Id,
                    field.FieldKey,
                    field.Label,
                    "required",
                    $"{field.Label} is required."));

                continue;
            }

            if (!isMissing &&
                value.ValueKind is not JsonValueKind.Null &&
                field.Type is FieldType.Text &&
                value.ValueKind is not JsonValueKind.String)
            {
                errors.Add(new ValidationError(
                    field.Id,
                    field.FieldKey,
                    field.Label,
                    "invalid_type",
                    $"{field.Label} must be text."));

                continue;
            }

            if (!isMissing &&
                value.ValueKind is JsonValueKind.String &&
                field.MinLength is not null &&
                value.GetString()!.Length < field.MinLength)
            {
                errors.Add(new ValidationError(
                    field.Id,
                    field.FieldKey,
                    field.Label,
                    "text_too_short",
                    $"{field.Label} must contain at least {field.MinLength} characters."));
            }

            if (!isMissing &&
                value.ValueKind is JsonValueKind.String &&
                field.MaxLength is not null &&
                value.GetString()!.Length > field.MaxLength)
            {
                errors.Add(new ValidationError(
                    field.Id,
                    field.FieldKey,
                    field.Label,
                    "text_too_long",
                    $"{field.Label} must contain at most {field.MaxLength} characters."));
            }

            if (!isMissing &&
                value.ValueKind is JsonValueKind.String &&
                field.TextFormat is TextFormat.Email &&
                !new EmailAddressAttribute().IsValid(value.GetString()))
            {
                errors.Add(new ValidationError(
                    field.Id,
                    field.FieldKey,
                    field.Label,
                    "invalid_email",
                    $"{field.Label} must be a valid email address."));
            }

            if (!isMissing &&
                value.ValueKind is not JsonValueKind.Null &&
                field.Type is FieldType.Number &&
                value.ValueKind is not JsonValueKind.Number)
            {
                errors.Add(new ValidationError(
                    field.Id,
                    field.FieldKey,
                    field.Label,
                    "invalid_type",
                    $"{field.Label} must be a number."));

                continue;
            }

            if (!isMissing &&
                value.ValueKind is JsonValueKind.Number &&
                field.Type is FieldType.Number &&
                field.NumberMode is NumberMode.Integer &&
                !value.TryGetInt64(out _))
            {
                errors.Add(new ValidationError(
                    field.Id,
                    field.FieldKey,
                    field.Label,
                    "invalid_integer",
                    $"{field.Label} must be an integer."));
            }

            if (!isMissing &&
                value.ValueKind is JsonValueKind.Number &&
                field.Type is FieldType.Number &&
                field.NumberMode is NumberMode.Decimal &&
                !value.TryGetDecimal(out _))
            {
                errors.Add(new ValidationError(
                    field.Id,
                    field.FieldKey,
                    field.Label,
                    "invalid_decimal",
                    $"{field.Label} must be a decimal number."));
            }

            if (!isMissing &&
                value.ValueKind is JsonValueKind.Number &&
                value.TryGetDecimal(out var numberValue) &&
                field.NumberMin is not null &&
                numberValue < field.NumberMin)
            {
                errors.Add(new ValidationError(
                    field.Id,
                    field.FieldKey,
                    field.Label,
                    "number_below_minimum",
                    $"{field.Label} must be at least {field.NumberMin}."));
            }

            if (!isMissing &&
                value.ValueKind is JsonValueKind.Number &&
                value.TryGetDecimal(out numberValue) &&
                field.NumberMax is not null &&
                numberValue > field.NumberMax)
            {
                errors.Add(new ValidationError(
                    field.Id,
                    field.FieldKey,
                    field.Label,
                    "number_above_maximum",
                    $"{field.Label} must be at most {field.NumberMax}."));
            }

            if (!isMissing &&
                value.ValueKind is JsonValueKind.String &&
                field.Type is FieldType.Date &&
                !DateOnly.TryParseExact(
                    value.GetString(),
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out _))
            {
                errors.Add(new ValidationError(
                    field.Id,
                    field.FieldKey,
                    field.Label,
                    "invalid_date",
                    $"{field.Label} must be a valid date in YYYY-MM-DD format."));
            }

            if (!isMissing &&
                value.ValueKind is not JsonValueKind.Null &&
                field.Type is FieldType.Date &&
                value.ValueKind is not JsonValueKind.String)
            {
                errors.Add(new ValidationError(
                    field.Id,
                    field.FieldKey,
                    field.Label,
                    "invalid_type",
                    $"{field.Label} must be a date string."));
            }

            if (!isMissing &&
                value.ValueKind is JsonValueKind.String &&
                field.Type is FieldType.SingleSelect &&
                field.SelectOptions.All(option => option.Value != value.GetString()))
            {
                errors.Add(new ValidationError(
                    field.Id,
                    field.FieldKey,
                    field.Label,
                    "invalid_option",
                    $"{field.Label} must contain a defined option value."));
            }

            if (!isMissing &&
                value.ValueKind is JsonValueKind.String &&
                field.Type is FieldType.SingleSelect &&
                field.SelectOptions.Any(option =>
                    option.Value == value.GetString() && !option.IsActive) &&
                !PreservesExistingValue(field.FieldKey, value, existingData))
            {
                errors.Add(new ValidationError(
                    field.Id,
                    field.FieldKey,
                    field.Label,
                    "inactive_option",
                    $"{field.Label} cannot use an inactive option."));
            }

            if (!isMissing &&
                value.ValueKind is not JsonValueKind.Null &&
                field.Type is FieldType.SingleSelect &&
                value.ValueKind is not JsonValueKind.String)
            {
                errors.Add(new ValidationError(
                    field.Id,
                    field.FieldKey,
                    field.Label,
                    "invalid_type",
                    $"{field.Label} must be a select option value."));
            }
        }

        return new ValidationResult(errors);
    }

    private static bool PreservesExistingValue(
        string fieldKey,
        JsonElement value,
        JsonElement? existingData) =>
        existingData is not null &&
        existingData.Value.ValueKind is JsonValueKind.Object &&
        existingData.Value.TryGetProperty(fieldKey, out var existingValue) &&
        JsonElement.DeepEquals(value, existingValue);
}
