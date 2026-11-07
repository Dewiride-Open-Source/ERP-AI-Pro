using Dewiride.Erp.BuildingBlocks.Persistence;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Persistence;

internal sealed class StatutoryParametersDbContext(DbContextOptions<StatutoryParametersDbContext> options) : ModuleDbContext(options, SchemaName)
{
    public const string SchemaName = "compliance_statutory_parameters";

    public DbSet<ParameterSet> ParameterSets => Set<ParameterSet>();

    public DbSet<ParameterVersion> ParameterVersions => Set<ParameterVersion>();

    public DbSet<ParameterRow> ParameterRows => Set<ParameterRow>();
}
