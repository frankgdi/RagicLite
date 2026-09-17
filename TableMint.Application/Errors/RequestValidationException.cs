namespace TableMint.Application.Errors;

public sealed class RequestValidationException(
    string code,
    string message) : Exception(message)
{
    public string Code { get; } = code;
}
