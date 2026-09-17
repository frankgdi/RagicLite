using System.Text.Json;
using TableMint.Application.Validation;
using TableMint.Domain.Tables;

namespace TableMint.Application.Tests.Validation;

public sealed class DynamicValidatorTests
{
    [Fact]
    public void Inactive_field_rejects_a_value_on_a_new_record()
    {
        var schema = new TableSchema(
            version: 2,
            fields:
            [
                FieldDefinition.CreateText(
                    Guid.NewGuid(),
                    "legacy_code",
                    "舊代碼",
                    required: false,
                    isActive: false),
            ]);
        using var document = JsonDocument.Parse("""{"legacy_code":"OLD"}""");

        var result = new DynamicValidator().Validate(schema, document.RootElement);

        Assert.Equal("inactive_field", Assert.Single(result.Errors).Code);
    }

    [Fact]
    public void Inactive_field_allows_its_unchanged_value_on_an_existing_record()
    {
        var schema = new TableSchema(
            version: 2,
            fields:
            [
                FieldDefinition.CreateText(
                    Guid.NewGuid(),
                    "legacy_code",
                    "舊代碼",
                    required: false,
                    isActive: false),
            ]);
        using var existing = JsonDocument.Parse("""{"legacy_code":"OLD"}""");
        using var merged = JsonDocument.Parse("""{"legacy_code":"OLD"}""");

        var result = new DynamicValidator().Validate(
            schema,
            merged.RootElement,
            existing.RootElement);

        Assert.True(result.IsValid);
    }
    [Fact]
    public void Required_text_field_reports_an_error_when_its_value_is_missing()
    {
        var fieldId = Guid.NewGuid();
        var schema = new TableSchema(
            version: 1,
            fields:
            [
                FieldDefinition.CreateText(
                    fieldId,
                    fieldKey: "customer_name",
                    label: "客戶名稱",
                    required: true),
            ]);
        using var document = JsonDocument.Parse("{}");

        var result = new DynamicValidator().Validate(schema, document.RootElement);

        var error = Assert.Single(result.Errors);
        Assert.Equal(fieldId, error.FieldId);
        Assert.Equal("客戶名稱", error.FieldLabel);
        Assert.Equal("required", error.Code);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("\"   \"")]
    public void Required_text_field_reports_an_error_when_its_value_is_blank(
        string jsonValue)
    {
        var schema = new TableSchema(
            version: 1,
            fields:
            [
                FieldDefinition.CreateText(
                    Guid.NewGuid(),
                    fieldKey: "customer_name",
                    label: "客戶名稱",
                    required: true),
            ]);
        using var document = JsonDocument.Parse($$"""{"customer_name":{{jsonValue}}}""");

        var result = new DynamicValidator().Validate(schema, document.RootElement);

        Assert.Equal("required", Assert.Single(result.Errors).Code);
    }

    [Fact]
    public void Text_field_rejects_a_json_number()
    {
        var schema = new TableSchema(
            version: 1,
            fields:
            [
                FieldDefinition.CreateText(
                    Guid.NewGuid(),
                    fieldKey: "customer_name",
                    label: "客戶名稱",
                    required: false),
            ]);
        using var document = JsonDocument.Parse("""{"customer_name":42}""");

        var result = new DynamicValidator().Validate(schema, document.RootElement);

        Assert.Equal("invalid_type", Assert.Single(result.Errors).Code);
    }

    [Fact]
    public void Text_field_rejects_a_value_shorter_than_its_minimum_length()
    {
        var schema = new TableSchema(
            version: 1,
            fields:
            [
                FieldDefinition.CreateText(
                    Guid.NewGuid(),
                    fieldKey: "customer_name",
                    label: "客戶名稱",
                    required: false,
                    minLength: 3),
            ]);
        using var document = JsonDocument.Parse("""{"customer_name":"Jo"}""");

        var result = new DynamicValidator().Validate(schema, document.RootElement);

        Assert.Equal("text_too_short", Assert.Single(result.Errors).Code);
    }

    [Fact]
    public void Text_field_rejects_a_value_longer_than_its_maximum_length()
    {
        var schema = new TableSchema(
            version: 1,
            fields:
            [
                FieldDefinition.CreateText(
                    Guid.NewGuid(),
                    fieldKey: "code",
                    label: "代碼",
                    required: false,
                    maxLength: 4),
            ]);
        using var document = JsonDocument.Parse("""{"code":"ABCDE"}""");

        var result = new DynamicValidator().Validate(schema, document.RootElement);

        Assert.Equal("text_too_long", Assert.Single(result.Errors).Code);
    }

    [Fact]
    public void Email_text_field_rejects_a_non_email_value()
    {
        var schema = new TableSchema(
            version: 1,
            fields:
            [
                FieldDefinition.CreateText(
                    Guid.NewGuid(),
                    fieldKey: "contact_email",
                    label: "聯絡信箱",
                    required: false,
                    format: TextFormat.Email),
            ]);
        using var document = JsonDocument.Parse("""{"contact_email":"not-an-email"}""");

        var result = new DynamicValidator().Validate(schema, document.RootElement);

        Assert.Equal("invalid_email", Assert.Single(result.Errors).Code);
    }

    [Fact]
    public void Record_rejects_a_field_key_that_is_not_in_the_schema()
    {
        var schema = new TableSchema(version: 1, fields: []);
        using var document = JsonDocument.Parse("""{"unexpected":"value"}""");

        var result = new DynamicValidator().Validate(schema, document.RootElement);

        var error = Assert.Single(result.Errors);
        Assert.Null(error.FieldId);
        Assert.Equal("unexpected", error.FieldKey);
        Assert.Equal("unknown_field", error.Code);
    }

    [Fact]
    public void Integer_number_field_rejects_a_fractional_number()
    {
        var schema = new TableSchema(
            version: 1,
            fields:
            [
                FieldDefinition.CreateNumber(
                    Guid.NewGuid(),
                    fieldKey: "quantity",
                    label: "數量",
                    required: false,
                    mode: NumberMode.Integer),
            ]);
        using var document = JsonDocument.Parse("""{"quantity":12.5}""");

        var result = new DynamicValidator().Validate(schema, document.RootElement);

        Assert.Equal("invalid_integer", Assert.Single(result.Errors).Code);
    }

    [Fact]
    public void Number_field_rejects_a_json_string()
    {
        var schema = new TableSchema(
            version: 1,
            fields:
            [
                FieldDefinition.CreateNumber(
                    Guid.NewGuid(),
                    fieldKey: "quantity",
                    label: "數量",
                    required: false,
                    mode: NumberMode.Integer),
            ]);
        using var document = JsonDocument.Parse("""{"quantity":"12"}""");

        var result = new DynamicValidator().Validate(schema, document.RootElement);

        Assert.Equal("invalid_type", Assert.Single(result.Errors).Code);
    }

    [Fact]
    public void Decimal_number_field_rejects_a_value_outside_decimal_range()
    {
        var schema = new TableSchema(
            version: 1,
            fields:
            [
                FieldDefinition.CreateNumber(
                    Guid.NewGuid(),
                    fieldKey: "amount",
                    label: "金額",
                    required: false,
                    mode: NumberMode.Decimal),
            ]);
        using var document = JsonDocument.Parse("""{"amount":1e1000}""");

        var result = new DynamicValidator().Validate(schema, document.RootElement);

        Assert.Equal("invalid_decimal", Assert.Single(result.Errors).Code);
    }

    [Theory]
    [InlineData("9", "number_below_minimum")]
    [InlineData("21", "number_above_maximum")]
    public void Number_field_rejects_a_value_outside_its_configured_range(
        string jsonValue,
        string expectedCode)
    {
        var schema = new TableSchema(
            version: 1,
            fields:
            [
                FieldDefinition.CreateNumber(
                    Guid.NewGuid(),
                    fieldKey: "score",
                    label: "分數",
                    required: false,
                    mode: NumberMode.Decimal,
                    min: 10m,
                    max: 20m),
            ]);
        using var document = JsonDocument.Parse($$"""{"score":{{jsonValue}}}""");

        var result = new DynamicValidator().Validate(schema, document.RootElement);

        Assert.Equal(expectedCode, Assert.Single(result.Errors).Code);
    }

    [Fact]
    public void Date_field_rejects_a_nonexistent_calendar_date()
    {
        var schema = new TableSchema(
            version: 1,
            fields:
            [
                FieldDefinition.CreateDate(
                    Guid.NewGuid(),
                    fieldKey: "due_date",
                    label: "到期日",
                    required: false),
            ]);
        using var document = JsonDocument.Parse("""{"due_date":"2026-02-30"}""");

        var result = new DynamicValidator().Validate(schema, document.RootElement);

        Assert.Equal("invalid_date", Assert.Single(result.Errors).Code);
    }

    [Fact]
    public void Date_field_rejects_a_non_string_value()
    {
        var schema = new TableSchema(
            version: 1,
            fields:
            [
                FieldDefinition.CreateDate(
                    Guid.NewGuid(),
                    fieldKey: "due_date",
                    label: "到期日",
                    required: false),
            ]);
        using var document = JsonDocument.Parse("""{"due_date":20260917}""");

        var result = new DynamicValidator().Validate(schema, document.RootElement);

        Assert.Equal("invalid_type", Assert.Single(result.Errors).Code);
    }

    [Fact]
    public void Single_select_field_rejects_a_value_that_is_not_an_option()
    {
        var schema = new TableSchema(
            version: 1,
            fields:
            [
                FieldDefinition.CreateSingleSelect(
                    Guid.NewGuid(),
                    fieldKey: "status",
                    label: "狀態",
                    required: false,
                    options:
                    [
                        new SelectOption("open", "開啟", IsActive: true),
                    ]),
            ]);
        using var document = JsonDocument.Parse("""{"status":"closed"}""");

        var result = new DynamicValidator().Validate(schema, document.RootElement);

        Assert.Equal("invalid_option", Assert.Single(result.Errors).Code);
    }

    [Fact]
    public void Single_select_field_rejects_an_inactive_option_for_a_new_value()
    {
        var schema = new TableSchema(
            version: 1,
            fields:
            [
                FieldDefinition.CreateSingleSelect(
                    Guid.NewGuid(),
                    fieldKey: "status",
                    label: "狀態",
                    required: false,
                    options:
                    [
                        new SelectOption("closed", "關閉", IsActive: false),
                    ]),
            ]);
        using var document = JsonDocument.Parse("""{"status":"closed"}""");

        var result = new DynamicValidator().Validate(schema, document.RootElement);

        Assert.Equal("inactive_option", Assert.Single(result.Errors).Code);
    }

    [Fact]
    public void Single_select_field_rejects_a_non_string_value()
    {
        var schema = new TableSchema(
            version: 1,
            fields:
            [
                FieldDefinition.CreateSingleSelect(
                    Guid.NewGuid(),
                    fieldKey: "status",
                    label: "狀態",
                    required: false,
                    options:
                    [
                        new SelectOption("open", "開啟", IsActive: true),
                    ]),
            ]);
        using var document = JsonDocument.Parse("""{"status":1}""");

        var result = new DynamicValidator().Validate(schema, document.RootElement);

        Assert.Equal("invalid_type", Assert.Single(result.Errors).Code);
    }

    [Fact]
    public void Record_data_must_be_a_json_object()
    {
        var schema = new TableSchema(version: 1, fields: []);
        using var document = JsonDocument.Parse("[]");

        var result = new DynamicValidator().Validate(schema, document.RootElement);

        var error = Assert.Single(result.Errors);
        Assert.Null(error.FieldId);
        Assert.Equal("$", error.FieldKey);
        Assert.Equal("invalid_record_data", error.Code);
    }
}
