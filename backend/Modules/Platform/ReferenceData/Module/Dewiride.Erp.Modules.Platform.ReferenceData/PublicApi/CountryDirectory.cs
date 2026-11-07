using Dewiride.Erp.Modules.Platform.ReferenceData.Contracts.Countries;
using Dewiride.Erp.Modules.Platform.ReferenceData.Countries.Domain;
using Dewiride.Erp.Modules.Platform.ReferenceData.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.PublicApi;

// The database compares text without regard to case, so a code that is not two capital letters is answered here, not there.
internal sealed class CountryDirectory(ReferenceDataDbContext context) : ICountries
{
    public async Task<IReadOnlyList<CountryEntry>> ListAsync(CancellationToken cancellationToken) =>
        await context.Countries
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .ThenBy(c => c.Code)
            .Select(c => new CountryEntry(c.Code, c.Name))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<CountryEntry?> FindAsync(string code, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(code);
        if (!Country.IsCode(code))
        {
            return null;
        }

        return await context.Countries
            .AsNoTracking()
            .Where(c => c.Code == code)
            .Select(c => new CountryEntry(c.Code, c.Name))
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
