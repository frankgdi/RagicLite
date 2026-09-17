using TableMint.Application.Validation;

namespace TableMint.Application.Errors;

public sealed class RecordValidationException(
    IReadOnlyList<ValidationError> errors) : Exception("Record data is invalid.")
{
    public IReadOnlyList<ValidationError> Errors { get; } = errors;
}
