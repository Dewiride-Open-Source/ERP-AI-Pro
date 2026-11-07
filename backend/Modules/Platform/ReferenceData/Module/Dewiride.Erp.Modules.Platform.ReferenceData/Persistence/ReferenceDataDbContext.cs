using Dewiride.Erp.BuildingBlocks.Persistence;
using Dewiride.Erp.Modules.Platform.ReferenceData.Countries.Domain;
using Dewiride.Erp.Modules.Platform.ReferenceData.ExchangeRates.Domain;
using Dewiride.Erp.Modules.Platform.ReferenceData.IndianStates.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.Persistence;

internal sealed class ReferenceDataDbContext(DbContextOptions<ReferenceDataDbContext> options) : ModuleDbContext(options, SchemaName)
{
    public const string SchemaName = "platform_reference_data";

    public DbSet<Country> Countries => Set<Country>();

    public DbSet<IndianState> IndianStates => Set<IndianState>();

    public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();
}
