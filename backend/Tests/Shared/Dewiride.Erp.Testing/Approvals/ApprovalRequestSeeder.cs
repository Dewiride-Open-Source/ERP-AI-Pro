using Dewiride.Erp.BuildingBlocks.Application.Events;
using Dewiride.Erp.BuildingBlocks.Approvals.Persistence;
using Dewiride.Erp.BuildingBlocks.Approvals.Submitting;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Kernel.Time;
using Dewiride.Erp.BuildingBlocks.Persistence.UnitOfWork;
using Microsoft.Extensions.DependencyInjection;

namespace Dewiride.Erp.Testing.Approvals;

// No module asks for approval yet, so a test submits a request of a test kind as a module's command would: in a unit of work on the
// approvals' own context, where the test kinds keep their requests, as the person the scope acts for, who is the person signed in to
// the request it runs in and the system outside one. The kind's setting applies, so a kind the administrators switched off answers
// Approved without a request. A request submitted some working days ago is submitted under a clock set back by that many days from
// Monday to Friday, keeping its time of day in India, so it is routed, dated and due as on that day: a step's manager is the one the
// asker's position names on that day, and a step of two working days submitted more than two working days ago is late, while the
// request's change records keep the moment they were written.
public static class ApprovalRequestSeeder
{
    public const int MaxWorkingDaysAgo = 30;

    public static async Task<Result<ApprovalSubmitted>> SubmitAsync(IServiceProvider services, ApprovalRequestSeed seed, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(seed);
        if (TestApprovalKinds.Find(seed.Kind) is null)
        {
            throw new ArgumentException($"'{seed.Kind}' is not a test kind of approval; the seeder submits the kinds of {nameof(TestApprovalKinds)}, whose requests the approvals' own context keeps.", nameof(seed));
        }

        if (seed.SubmittedWorkingDaysAgo is < 0 or > MaxWorkingDaysAgo)
        {
            throw new ArgumentException($"A seeded request is submitted 0 to {MaxWorkingDaysAgo} working days ago, not {seed.SubmittedWorkingDaysAgo}.", nameof(seed));
        }

        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var context = provider.GetRequiredService<ApprovalsDbContext>();
        var submittedAt = SubmittedAt(provider.GetRequiredService<TimeProvider>().GetUtcNow(), seed.SubmittedWorkingDaysAgo);
        var approvals = ActivatorUtilities.CreateInstance<RecordApprovals>(provider, new SubmissionClock(submittedAt));

        return await EfUnitOfWork.RunAsync<ApprovalSubmitted>(
            context,
            provider.GetRequiredService<DomainEventDispatcher>(),
            provider.GetRequiredService<UnitOfWorkSignal>(),
            token => approvals.SubmitAsync(context, seed.Kind, seed.SubjectId, seed.RequestedFor, token),
            cancellationToken).ConfigureAwait(false);
    }

    public static DateTimeOffset SubmittedAt(DateTimeOffset now, int workingDaysAgo)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(workingDaysAgo);

        var inIndia = IndianStandardTime.ToIst(now);
        var day = DateOnly.FromDateTime(inIndia.DateTime);
        for (var counted = 0; counted < workingDaysAgo;)
        {
            day = day.AddDays(-1);
            if (day.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            {
                counted++;
            }
        }

        return new DateTimeOffset(day.ToDateTime(TimeOnly.FromTimeSpan(inIndia.TimeOfDay)), IndianStandardTime.Offset).ToUniversalTime();
    }

    private sealed class SubmissionClock(DateTimeOffset submittedAt) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => submittedAt;
    }
}
