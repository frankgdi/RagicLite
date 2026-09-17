using TableMint.Application.Abstractions;

namespace TableMint.WebApi.Runtime;

public sealed class ConfiguredTenantContext : ITenantContext
{
    public ConfiguredTenantContext(IConfiguration configuration)
    {
        var configuredId = configuration["TableMint:DefaultTenantId"];

        if (!Guid.TryParse(configuredId, out var tenantId))
        {
            throw new InvalidOperationException(
                "TableMint:DefaultTenantId must be a valid GUID.");
        }

        TenantId = tenantId;
    }

    public Guid TenantId { get; }
}
