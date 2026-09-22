using Dewiride.Erp.BuildingBlocks.Kernel.Domain;
using Dewiride.Erp.BuildingBlocks.Persistence.Conventions;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.Persistence;

public abstract class ModuleDbContext : DbContext
{
    public const int MoneyPrecision = 19;

    public const int MoneyScale = 4;

    protected ModuleDbContext(DbContextOptions options, string schema)
        : base(options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);

        Schema = schema;
    }

    public string Schema { get; }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        configurationBuilder.Properties<decimal>().HavePrecision(MoneyPrecision, MoneyScale);
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<UtcDateTimeOffsetConverter>();
        configurationBuilder.IgnoreAny<IDomainEvent>();
        foreach (var idType in StronglyTypedIdTypes.In(GetType().Assembly))
        {
            configurationBuilder.Properties(idType).HaveConversion(typeof(StronglyTypedIdConverter<>).MakeGenericType(idType));
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);
    }
}
