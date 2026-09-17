namespace TableMint.Application.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
