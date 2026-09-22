using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dewiride.Erp.BuildingBlocks.Persistence.Options;

internal sealed class DatabaseOptionsValidator : IValidateOptions<DatabaseOptions>
{
    public ValidateOptionsResult Validate(string? name, DatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return string.IsNullOrWhiteSpace(options.ConnectionString) && !EF.IsDesignTime
            ? ValidateOptionsResult.Fail(
                $"{DatabaseOptions.SectionName}:ConnectionString is required. It comes from the Key Vault reference Erp--Platform--Database--ConnectionString through App Configuration, " +
                "from dotnet user-secrets on a developer machine, from the environment variable Erp__Platform__Database__ConnectionString in CI, " +
                "or from the secret file /run/secrets/Erp__Platform__Database__ConnectionString in a container.")
            : ValidateOptionsResult.Success;
    }
}
