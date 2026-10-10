using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Persistence.UnitOfWork;
using Dewiride.Erp.Modules.Identity.Users.People.Application.Commands.AdmitPerson;
using Dewiride.Erp.Modules.Identity.Users.People.Application.Queries.IsPersonAdmitted;
using Microsoft.Extensions.DependencyInjection;

namespace Dewiride.Erp.Modules.Identity.Users.Integration;

// Sign-ins of one person can run at once, in two tabs or two browsers: the later save then finds the record changed since it
// was read, or, at the person's first sign-in, already created, so the admission tries again in a scope of its own, which
// reads the record the other sign-in saved. Sign-ins that keep winning have brought the record up to date themselves, so
// after the last attempt the admission only reads whether the record admits the person. Any other failure to read or write
// the record is thrown and fails the sign-in or the request instead of admitting the person.
internal sealed class UserAdmission(IServiceScopeFactory scopes, IQueryHandler<IsPersonAdmittedQuery, bool> isAdmitted) : IPersonAdmission
{
    private const int Attempts = 5;

    public async Task<bool> AdmitAsync(SignedInPerson person, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(person);

        var command = new AdmitPersonCommand(person.ObjectId, person.Name, person.UserName);
        for (var attempt = 1; ; attempt++)
        {
            await using var scope = scopes.CreateAsyncScope();
            var admit = scope.ServiceProvider.GetRequiredService<ICommandHandler<AdmitPersonCommand, bool>>();
            var result = await admit.HandleAsync(command, cancellationToken).ConfigureAwait(false);
            if (result.IsSuccess)
            {
                return result.Value;
            }

            if (result.Error!.Code != EfUnitOfWork.ConcurrencyConflictCode)
            {
                throw new InvalidOperationException($"The person's record could not be brought up to date from the sign-in: {result.Error.Code}.");
            }

            if (attempt == Attempts)
            {
                return await IsAdmittedAsync(person.ObjectId, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public async Task<bool> IsAdmittedAsync(Guid objectId, CancellationToken cancellationToken)
    {
        var result = await isAdmitted.HandleAsync(new IsPersonAdmittedQuery(objectId), cancellationToken).ConfigureAwait(false);

        return result.IsSuccess
            ? result.Value
            : throw new InvalidOperationException($"The person's record could not be read: {result.Error!.Code}.");
    }
}
