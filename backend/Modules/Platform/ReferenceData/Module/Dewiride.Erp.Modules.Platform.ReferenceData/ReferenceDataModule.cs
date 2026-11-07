using Dewiride.Erp.BuildingBlocks.Application.DependencyInjection;
using Dewiride.Erp.BuildingBlocks.Kernel.Access;
using Dewiride.Erp.BuildingBlocks.Modules;
using Dewiride.Erp.BuildingBlocks.Persistence;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Dewiride.Erp.BuildingBlocks.Persistence.Seeding;
using Dewiride.Erp.Modules.Platform.ReferenceData.Contracts;
using Dewiride.Erp.Modules.Platform.ReferenceData.Contracts.Countries;
using Dewiride.Erp.Modules.Platform.ReferenceData.Contracts.ExchangeRates;
using Dewiride.Erp.Modules.Platform.ReferenceData.Contracts.IndianStates;
using Dewiride.Erp.Modules.Platform.ReferenceData.Countries.Endpoints;
using Dewiride.Erp.Modules.Platform.ReferenceData.Countries.Persistence;
using Dewiride.Erp.Modules.Platform.ReferenceData.Currencies.Endpoints;
using Dewiride.Erp.Modules.Platform.ReferenceData.ExchangeRates.Endpoints;
using Dewiride.Erp.Modules.Platform.ReferenceData.FinancialYears.Endpoints;
using Dewiride.Erp.Modules.Platform.ReferenceData.IndianStates.Endpoints;
using Dewiride.Erp.Modules.Platform.ReferenceData.IndianStates.Persistence;
using Dewiride.Erp.Modules.Platform.ReferenceData.Integration;
using Dewiride.Erp.Modules.Platform.ReferenceData.Persistence;
using Dewiride.Erp.Modules.Platform.ReferenceData.PublicApi;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Dewiride.Erp.Modules.Platform.ReferenceData;

public sealed class ReferenceDataModule : IModule
{
    public const string FeatureFlag = "Erp.Modules.Platform.ReferenceData";

    public ModuleDescriptor Descriptor { get; } = new(
        Domain: "Platform",
        Name: "ReferenceData",
        Schema: ReferenceDataDbContext.SchemaName,
        RoutePrefix: "/platform/reference-data",
        FeatureFlag: FeatureFlag,
        Permissions:
        [
            new(ReferenceDataPermissions.ReadLists, [ErpRole.Employee], "See the lists of countries, Indian states and union territories with their GST codes, currencies and financial years."),
            new(ReferenceDataPermissions.ReadExchangeRates, [ErpRole.Finance], "See the exchange rates recorded for foreign currencies."),
            new(ReferenceDataPermissions.RecordExchangeRates, [ErpRole.Finance], "Record exchange rates of foreign currencies, one at a time or by importing a file."),
            new(ReferenceDataPermissions.CorrectExchangeRates, [ErpRole.Finance], "Correct or remove an exchange rate recorded by mistake."),
        ],
        Capabilities: []);

    public void AddServices(IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddModuleDbContext<ReferenceDataDbContext>(ReferenceDataDbContext.SchemaName);
        builder.Services.AddHandlersFromAssembly(typeof(ReferenceDataModule).Assembly);
        builder.Services.AddScoped<ICountries, CountryDirectory>();
        builder.Services.AddScoped<IIndianStates, IndianStateDirectory>();
        builder.Services.AddScoped<IExchangeRates, ExchangeRateLookup>();
        builder.Services.AddSeeder<CountrySeeder>(100);
        builder.Services.AddSeeder<IndianStateSeeder>(110);
        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IRecordNames, CountryRecordNames>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IRecordNames, IndianStateRecordNames>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IRecordNames, ExchangeRateRecordNames>());
        builder.Services.AddValidation();
    }

    public void MapEndpoints(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        CountryEndpoints.Map(group);
        IndianStateEndpoints.Map(group);
        CurrencyEndpoints.Map(group);
        FinancialYearEndpoints.Map(group);
        ExchangeRateEndpoints.Map(group);
    }
}
