using System.Globalization;
using System.Text;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Persistence.Seeding;
using Dewiride.Erp.Modules.Platform.ReferenceData.Contracts.IndianStates;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.UnitTests.ParameterSets.Seeding;

// A probe seed file that keeps every rule, which a test breaks in one place, and GST State codes that are every two-digit code
// on every day, so a unit test checks the shape of a file and the reference data's own codes are the integration tests'.
internal static class SeedProbes
{
    public const string Key = "test.probe-rates";

    public static IReadOnlyList<IndianStateEntry> AnyStateCode { get; } =
        [.. Enumerable.Range(0, 100).Select(code => new IndianStateEntry(code.ToString("00", CultureInfo.InvariantCulture), "Probe territory", IndianStateKind.State, DateOnly.MinValue, null))];

    public static string File(string key = Key) => $$"""
        {
          "key": "{{key}}",
          "title": "Probe rates",
          "description": "A set the tests write.",
          "basis": "law",
          "asOf": "supplyDate",
          "columns": [
            { "name": "entry", "title": "Entry", "kind": "code", "key": true },
            { "name": "rate", "title": "Rate", "kind": "percent", "minimum": 0, "maximum": 100 },
            { "name": "state", "title": "State", "kind": "stateCode", "optional": true },
            { "name": "parent", "title": "Parent", "kind": "code", "optional": true, "references": ["{{key}}"] }
          ],
          "versions": [
            { "from": "2017-07-01", "instruments": ["Probe instrument one"] },
            { "from": "2019-10-01", "instruments": ["Probe instrument two"] }
          ],
          "rows": [
            { "from": "2017-07-01", "until": "2019-09-30", "source": "Probe source one", "cells": { "entry": "a", "rate": 9, "state": "27", "parent": null } },
            { "from": "2019-10-01", "source": "Probe source two", "cells": { "entry": "a", "rate": 18, "state": "27", "parent": null } },
            { "from": "2017-07-01", "source": "Probe source three", "cells": { "entry": "b", "rate": 2.5, "state": null, "parent": "a" } }
          ]
        }
        """;

    public static string Broken(string original, string replacement)
    {
        var file = File();
        Assert.Contains(original, file, StringComparison.Ordinal);

        return file.Replace(original, replacement, StringComparison.Ordinal);
    }

    public static StatutorySeedSource Source(string text, string? fileName = null) => new(fileName ?? Key + StatutorySeedFiles.FileExtension, Encoding.UTF8.GetBytes(text));

    public static IReadOnlyList<SeededSet> Read(params StatutorySeedSource[] sources) => StatutorySeedFiles.Read(sources, AnyStateCode);

    public static string Refusal(params StatutorySeedSource[] sources) =>
        Assert.Throws<InvalidOperationException>(() => StatutorySeedFiles.Read(sources, AnyStateCode)).Message;
}
