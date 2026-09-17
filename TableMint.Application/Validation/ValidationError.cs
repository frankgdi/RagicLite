namespace TableMint.Application.Validation;

public sealed record ValidationError(
    Guid? FieldId,
    string FieldKey,
    string FieldLabel,
    string Code,
    string Message);
