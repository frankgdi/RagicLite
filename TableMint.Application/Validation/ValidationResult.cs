namespace TableMint.Application.Validation;

public sealed class ValidationResult
{
    public ValidationResult(IReadOnlyList<ValidationError> errors)
    {
        Errors = errors;
    }

    public IReadOnlyList<ValidationError> Errors { get; }

    public bool IsValid => Errors.Count == 0;
}
