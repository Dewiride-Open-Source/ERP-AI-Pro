using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dewiride.Erp.Modules.Platform.ReferenceData.Persistence;

// The seed files are embedded in this assembly under their file names and read strictly, so a file whose shape changed
// stops the migrator instead of seeding part of it.
internal static class SeedFiles
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    public static T Read<T>(string fileName)
    {
        var assembly = typeof(SeedFiles).Assembly;
        using var stream = assembly.GetManifestResourceStream(fileName)
            ?? throw new InvalidOperationException($"The seed file '{fileName}' is not embedded in {assembly.GetName().Name}.");

        return JsonSerializer.Deserialize<T>(stream, Options)
            ?? throw new InvalidOperationException($"The seed file '{fileName}' holds no value.");
    }
}
