using TableMint.Application.Abstractions;

namespace TableMint.WebApi.Runtime;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
