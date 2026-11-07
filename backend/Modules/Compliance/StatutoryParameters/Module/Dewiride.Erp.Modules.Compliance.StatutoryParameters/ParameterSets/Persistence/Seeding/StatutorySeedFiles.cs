using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Domain;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Domain.Columns;
using Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Domain.Timeline;
using Dewiride.Erp.Modules.Platform.ReferenceData.Contracts.IndianStates;

namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.ParameterSets.Persistence.Seeding;

// Every seed file is embedded in this assembly as statutory/<key>.json, read strictly and checked whole, and every defect of every
// file is reported together before anything is written: a member the shape does not know, a member written twice, a number in
// quotes, a value that breaks its column, a row off a version's boundary, rows of a key that overlap, a version that changes
// nothing and a reference or GST State code that does not hold on every day of its row. A defect stops the migrator, and with it
// the deployment, before any write.
internal static class StatutorySeedFiles
{
    public const string ResourcePrefix = "statutory/";

    public const string FileExtension = ".json";

    private static readonly JsonSerializerOptions Options = CreateOptions();

    public static IReadOnlyList<StatutorySeedSource> Embedded()
    {
        var assembly = typeof(StatutorySeedFiles).Assembly;

        return [.. assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal) && name.EndsWith(FileExtension, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .Select(name => new StatutorySeedSource(name[ResourcePrefix.Length..], ReadResource(assembly, name)))];
    }

    public static IReadOnlyList<SeededSet> Read(IReadOnlyList<StatutorySeedSource> sources, IReadOnlyList<IndianStateEntry> states)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(states);

        var problems = new List<string>();
        var sets = new List<SeededSet>();
        foreach (var source in sources)
        {
            if (Parse(source, problems) is { } seeded)
            {
                sets.Add(seeded);
            }
        }

        problems.AddRange(sets
            .GroupBy(seeded => seeded.Set.Key, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key}{FileExtension}: {group.Count()} seed files hold the set {group.Key}."));
        if (problems.Count == 0)
        {
            problems.AddRange(SeedCoverage.Problems(sets, states));
        }

        return problems.Count == 0
            ? sets
            : throw new InvalidOperationException($"The statutory seed files were refused and nothing was written:{Environment.NewLine}{string.Join(Environment.NewLine, problems)}");
    }

    private static SeededSet? Parse(StatutorySeedSource source, List<string> problems)
    {
        StatutorySeedFile? file;
        try
        {
            file = JsonSerializer.Deserialize<StatutorySeedFile>(source.Content.Span, Options);
        }
        catch (JsonException exception)
        {
            problems.Add($"{source.FileName}: {exception.Message}");
            return null;
        }

        var found = problems.Count;
        void Problem(string where, string message) => problems.Add($"{source.FileName}: {where}: {message}");

        if (file is null)
        {
            Problem("the file", "it holds no set.");
            return null;
        }

        if (!ParameterSet.IsKey(file.Key))
        {
            Problem("key", $"'{file.Key}' breaks the set key pattern.");
        }
        else if (!string.Equals(source.FileName, file.Key + FileExtension, StringComparison.Ordinal))
        {
            Problem("key", $"'{file.Key}' is not the file's name.");
        }

        var basis = Named<SetBasis>(file.Basis);
        if (basis is null)
        {
            Problem("basis", $"'{file.Basis}' is neither law nor interpretation.");
        }

        var asOf = Named<AsOfDate>(file.AsOf);
        if (asOf is null)
        {
            Problem("asOf", $"'{file.AsOf}' is neither supplyDate nor taxPeriodStart.");
        }

        var references = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        var columns = new List<ParameterColumn>();
        foreach (var (column, index) in file.Columns.Select((column, index) => (column, index)))
        {
            if (Column(column, references, message => Problem($"column {index + 1}", message)) is { } parsed)
            {
                columns.Add(parsed);
            }
        }

        if (problems.Count > found || basis is null || asOf is null)
        {
            return null;
        }

        ParameterSet set;
        try
        {
            set = ParameterSet.Create(file.Key, file.Title, file.Description, basis.Value, asOf.Value, ParameterColumnSet.Create(columns));
        }
        catch (ArgumentException exception)
        {
            Problem("the set", MessageOf(exception));
            return null;
        }

        var versions = new List<ParameterVersion>();
        foreach (var (version, index) in file.Versions.Select((version, index) => (version, index)))
        {
            Try($"version {index + 1}", () => versions.Add(version is null
                ? throw new ArgumentException("it is null.", nameof(source))
                : ParameterVersion.Create(set, version.From, version.Instruments)));
        }

        var rows = new List<ParameterRow>();
        foreach (var (row, index) in file.Rows.Select((row, index) => (row, index)))
        {
            Try($"row {index + 1}", () => rows.Add(row is null
                ? throw new ArgumentException("it is null.", nameof(source))
                : ParameterRow.Create(set, row.From, row.Until, Cells(set.Columns, row.Cells), row.Source)));
        }

        if (problems.Count == found)
        {
            foreach (var problem in new ParameterTimeline(file.Versions.Select(version => version.From), rows).Problems())
            {
                Problem("versions and rows", problem);
            }
        }

        return problems.Count > found ? null : new SeededSet(set, versions, rows, references);

        void Try(string where, Action read)
        {
            try
            {
                read();
            }
            catch (ArgumentException exception)
            {
                Problem(where, MessageOf(exception));
            }
        }
    }

    private static ParameterColumn? Column(SeedColumn? column, Dictionary<string, IReadOnlyList<string>> references, Action<string> problem)
    {
        if (column is null)
        {
            problem("it is null.");
            return null;
        }

        var kind = Named<CellKind>(column.Kind);
        var valid = true;
        void Refuse(string message)
        {
            problem(message);
            valid = false;
        }

        if (kind is null)
        {
            Refuse($"the kind '{column.Kind}' is not one of the store's.");
        }

        foreach (var (flag, value) in new[] { ("key", column.Key), ("rowTitle", column.RowTitle), ("optional", column.Optional) })
        {
            if (value is false)
            {
                Refuse($"\"{flag}\" is written but not true; a flag is written only when true.");
            }
        }

        if (column.Choices is not null && kind is not CellKind.Choice)
        {
            Refuse("only a choice column declares choices.");
        }

        if (column.References is { } named)
        {
            if (kind is not CellKind.Code)
            {
                Refuse("only a code column declares references.");
            }

            if (named.Count == 0 || named.Distinct(StringComparer.Ordinal).Count() != named.Count || named.Any(key => !ParameterSet.IsKey(key)))
            {
                Refuse("its references are not one or more set keys, each named once.");
            }

            references[column.Name] = named;
        }

        if (!valid || kind is null)
        {
            return null;
        }

        try
        {
            return new ParameterColumn(
                column.Name,
                column.Title,
                kind.Value,
                column.Key is true,
                column.RowTitle is true,
                column.Optional is true,
                column.Minimum,
                column.Maximum,
                column.Choices?.Select(choice => choice is null ? null! : new ParameterChoice(choice.Value, choice.Title)) ?? []);
        }
        catch (ArgumentException exception)
        {
            problem(MessageOf(exception));
            return null;
        }
    }

    private static ParameterCells Cells(ParameterColumnSet columns, JsonElement cells)
    {
        if (cells.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException($"the cells are a JSON object, not {cells.ValueKind}.", nameof(cells));
        }

        var read = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var cell in cells.EnumerateObject())
        {
            var column = columns.Find(cell.Name) ?? throw new ArgumentException($"cell {cell.Name}: the set declares no such column.", nameof(cells));
            if (!read.TryAdd(cell.Name, SeedCells.Read(column, cell.Value)))
            {
                throw new ArgumentException($"cell {cell.Name} is written twice.", nameof(cells));
            }
        }

        if (columns.Columns.FirstOrDefault(column => !read.ContainsKey(column.Name)) is { } missing)
        {
            throw new ArgumentException($"cell {missing.Name} is missing; every row writes every column's cell, null where an optional column is empty.", nameof(cells));
        }

        return ParameterCells.Of(read);
    }

    private static string MessageOf(ArgumentException exception) =>
        exception.ParamName is { } name ? exception.Message.Replace($" (Parameter '{name}')", string.Empty, StringComparison.Ordinal) : exception.Message;

    private static TEnum? Named<TEnum>(string? name)
        where TEnum : struct, Enum =>
        Enum.GetValues<TEnum>()
            .Where(member => string.Equals(JsonNamingPolicy.CamelCase.ConvertName(member.ToString()), name, StringComparison.Ordinal))
            .Select(member => (TEnum?)member)
            .FirstOrDefault();

    private static byte[] ReadResource(Assembly assembly, string name)
    {
        using var stream = assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"The seed file '{name}' is not embedded in {assembly.GetName().Name}.");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);

        return buffer.ToArray();
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = false,
            NumberHandling = JsonNumberHandling.Strict,
            RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            AllowDuplicateProperties = false,
        };
        options.MakeReadOnly(populateMissingResolver: true);

        return options;
    }
}
