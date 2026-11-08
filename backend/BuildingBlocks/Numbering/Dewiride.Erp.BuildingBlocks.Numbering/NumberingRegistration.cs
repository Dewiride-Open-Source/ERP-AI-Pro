using Dewiride.Erp.BuildingBlocks.Numbering.Administration;
using Dewiride.Erp.BuildingBlocks.Numbering.Allocation;
using Dewiride.Erp.BuildingBlocks.Numbering.Definitions;
using Dewiride.Erp.BuildingBlocks.Numbering.Persistence;
using Dewiride.Erp.BuildingBlocks.Persistence;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Dewiride.Erp.BuildingBlocks.Numbering;

// The rule a kind of document's numbers follow is a statutory parameter, so the module that reads the parameter store registers
// ISerialNumberRules; the numbers and their administration need it.
public static class NumberingRegistration
{
    public static IHostApplicationBuilder AddErpNumbering(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddModuleDbContext<NumberingDbContext>(NumberingDbContext.SchemaName);
        builder.Services.TryAddSingleton(provider => new NumberedDocumentCatalog(provider.GetServices<NumberedDocumentRegistration>()));
        builder.Services.TryAddScoped<IDocumentNumbers, DocumentNumbers>();
        builder.Services.TryAddScoped<INumberingAdministration, NumberingAdministration>();
        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IRecordNames, NumberingRecordNames>());

        return builder;
    }
}
