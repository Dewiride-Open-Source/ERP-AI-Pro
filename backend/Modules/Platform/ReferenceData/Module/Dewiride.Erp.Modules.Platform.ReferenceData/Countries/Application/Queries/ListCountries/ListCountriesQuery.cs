using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.Modules.Platform.ReferenceData.Contracts.Countries;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.Countries.Application.Queries.ListCountries;

internal sealed record ListCountriesQuery : IQuery<IReadOnlyList<CountryEntry>>;
