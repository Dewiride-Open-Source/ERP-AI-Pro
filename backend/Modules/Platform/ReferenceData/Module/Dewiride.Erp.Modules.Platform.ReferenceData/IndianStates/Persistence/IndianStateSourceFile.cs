using Dewiride.Erp.Modules.Platform.ReferenceData.IndianStates.Domain;
using Dewiride.Erp.Modules.Platform.ReferenceData.Persistence;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.IndianStates.Persistence;

// The rows of one code may follow each other but never overlap, so a code names one territory on any date.
internal sealed record IndianStateSourceFile(string Source, IReadOnlyList<IndianStateSourceEntry> States)
{
    public const string FileName = "indian-states.json";

    public static IndianStateSourceFile Read()
    {
        var file = SeedFiles.Read<IndianStateSourceFile>(FileName);
        foreach (var rows in file.States.GroupBy(entry => entry.GstCode, StringComparer.Ordinal))
        {
            if (!IndianState.IsGstCode(rows.Key))
            {
                throw new InvalidOperationException($"{FileName} holds '{rows.Key}', which is not a GST State code of two digits.");
            }

            var ordered = rows.OrderBy(entry => entry.ValidFrom).ToList();
            for (var index = 1; index < ordered.Count; index++)
            {
                if (ordered[index - 1].ValidUntil is not { } until || until >= ordered[index].ValidFrom)
                {
                    throw new InvalidOperationException($"{FileName} holds rows of State code {rows.Key} that overlap from {ordered[index].ValidFrom:yyyy-MM-dd}.");
                }
            }
        }

        return file;
    }
}
