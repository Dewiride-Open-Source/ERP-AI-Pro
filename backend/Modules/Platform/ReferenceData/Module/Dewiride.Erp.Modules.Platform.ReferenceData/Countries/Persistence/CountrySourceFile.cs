using Dewiride.Erp.Modules.Platform.ReferenceData.Countries.Domain;
using Dewiride.Erp.Modules.Platform.ReferenceData.Persistence;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.Countries.Persistence;

internal sealed record CountrySourceFile(string Source, IReadOnlyList<CountrySourceEntry> Countries)
{
    public const string FileName = "countries.json";

    public static CountrySourceFile Read()
    {
        var file = SeedFiles.Read<CountrySourceFile>(FileName);
        var codes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in file.Countries)
        {
            if (!Country.IsCode(entry.Code) || !Country.IsName(entry.Name) || !codes.Add(entry.Code))
            {
                throw new InvalidOperationException($"{FileName} holds a malformed or repeated country '{entry.Code}' named '{entry.Name}'.");
            }
        }

        return file;
    }
}
