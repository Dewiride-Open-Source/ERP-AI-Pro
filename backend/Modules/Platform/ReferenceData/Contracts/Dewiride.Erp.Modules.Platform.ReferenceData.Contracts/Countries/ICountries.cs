namespace Dewiride.Erp.Modules.Platform.ReferenceData.Contracts.Countries;

public interface ICountries
{
    Task<IReadOnlyList<CountryEntry>> ListAsync(CancellationToken cancellationToken);

    Task<CountryEntry?> FindAsync(string code, CancellationToken cancellationToken);
}
