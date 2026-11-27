using Dewiride.Erp.BuildingBlocks.Approvals.Outcomes;
using Dewiride.Erp.BuildingBlocks.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Dewiride.Erp.BuildingBlocks.Approvals.Definitions;

// A kind names the context its requests are saved in, the module's own, which maps the approval tables and commits the record
// they approve, and the handler that moves that record when a request is decided, which is created for each decision and never
// registered.
public static class ApprovalsServiceCollectionExtensions
{
    public static IServiceCollection AddApprovalKind<TContext, THandler>(this IServiceCollection services, ApprovalKind kind)
        where TContext : ModuleDbContext
        where THandler : class, IApprovalOutcomeHandler
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(kind);

        kind.EnsureValid();
        if (typeof(THandler).IsAbstract)
        {
            throw new ArgumentException($"The approval kind {kind.Key} names {typeof(THandler).Name}, which is abstract; name the sealed handler that is created for each decision.");
        }

        var declared = services
            .Where(descriptor => descriptor.ServiceType == typeof(ApprovalKindRegistration) && !descriptor.IsKeyedService)
            .Select(descriptor => descriptor.ImplementationInstance)
            .OfType<ApprovalKindRegistration>()
            .Any(registration => string.Equals(registration.Kind.Key, kind.Key, StringComparison.Ordinal));
        if (declared)
        {
            throw new InvalidOperationException($"The approval kind {kind.Key} is declared more than once.");
        }

        services.AddSingleton(new ApprovalKindRegistration(kind, typeof(TContext), typeof(THandler)));

        return services;
    }
}
