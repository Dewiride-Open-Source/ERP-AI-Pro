using Microsoft.Extensions.DependencyInjection;

namespace Dewiride.Erp.BuildingBlocks.Numbering.Definitions;

public static class NumberingServiceCollectionExtensions
{
    public static IServiceCollection AddNumberedDocument(this IServiceCollection services, NumberedDocument document)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(document);

        document.EnsureValid();
        var declared = services
            .Where(descriptor => descriptor.ServiceType == typeof(NumberedDocumentRegistration) && !descriptor.IsKeyedService)
            .Select(descriptor => descriptor.ImplementationInstance)
            .OfType<NumberedDocumentRegistration>()
            .Any(registration => string.Equals(registration.Document.Key, document.Key, StringComparison.Ordinal));
        if (declared)
        {
            throw new InvalidOperationException($"The numbered document {document.Key} is declared more than once.");
        }

        services.AddSingleton(new NumberedDocumentRegistration(document));

        return services;
    }
}
