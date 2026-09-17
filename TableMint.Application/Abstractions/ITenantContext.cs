namespace TableMint.Application.Abstractions;

public interface ITenantContext
{
    Guid TenantId { get; }
}
