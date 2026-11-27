using Dewiride.Erp.Modules.Identity.Users.Contracts.Positions;
using Dewiride.Erp.Modules.Identity.Users.IntegrationTests.People.Endpoints;
using Dewiride.Erp.Modules.Identity.Users.IntegrationTests.Shared;
using Dewiride.Erp.Modules.Identity.Users.People.Domain;
using Dewiride.Erp.Modules.Identity.Users.Persistence;
using Dewiride.Erp.Modules.Identity.Users.Positions.Domain;
using Dewiride.Erp.Testing;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Identity.Users.IntegrationTests.PublicApi;

// The tests save the people and positions they read through the context, as a sign-in and the positions routes save them, so a
// test can also write the rows those routes refuse, such as reporting lines that loop.
public sealed class ReportingLineDirectoryTests(ErpApiFactory factory) : IClassFixture<ErpApiFactory>
{
    private static readonly DateOnly January = new(2026, 1, 1);

    private static readonly DateOnly April = new(2026, 4, 1);

    private static readonly DateOnly July = new(2026, 7, 1);

    [Fact]
    public async Task ManagersOfAsync_ObjectIdNoRecordIsLinkedTo_IsEmpty()
    {
        Assert.Empty(await ManagersOfAsync(Guid.CreateVersion7(), April));
    }

    [Fact]
    public async Task ManagersOfAsync_ObjectIdWhoseRecordWasDeleted_FollowsOnlyTheRecordNotDeleted()
    {
        var objectId = Guid.CreateVersion7();
        var departed = SignedIn("Kavya Iyer", objectId);
        var formerLead = SignedIn("Rohan Das");
        await SaveAsync(departed, formerLead, Reports(departed, January, formerLead));
        await DeleteAsync(departed);

        Assert.Empty(await ManagersOfAsync(objectId, April));

        var returned = SignedIn("Kavya Iyer", objectId);
        var lead = SignedIn("Nisha Menon");
        await SaveAsync(returned, lead, Reports(returned, January, lead));

        Assert.Equal([Active(lead)], await ManagersOfAsync(objectId, April));
    }

    [Fact]
    public async Task ManagersOfAsync_PersonWithoutAPosition_IsEmpty()
    {
        var person = SignedIn("Kavya Iyer");
        await SaveAsync(person);

        Assert.Empty(await ManagersOfAsync(person, April));
    }

    [Fact]
    public async Task ManagersOfAsync_PositionOnTheDateThatNamesNoManager_IsEmptyWhateverAnEarlierOneNamed()
    {
        var person = SignedIn("Kavya Iyer");
        var lead = SignedIn("Rohan Das");
        await SaveAsync(person, lead, Reports(person, January, lead), Reports(person, April, null));

        Assert.Equal([Active(lead)], await ManagersOfAsync(person, April.AddDays(-1)));
        Assert.Empty(await ManagersOfAsync(person, April));
    }

    [Fact]
    public async Task ManagersOfAsync_ManagersOnADate_ListsThemNearestFirstEachWithTheirManagerOnThatDate()
    {
        var person = SignedIn("Kavya Iyer");
        var lead = SignedIn("Rohan Das");
        var head = SignedIn("Nisha Menon");
        var director = SignedIn("Arjun Rao");
        await SaveAsync(
            person,
            lead,
            head,
            director,
            Reports(person, January, lead),
            Reports(lead, January, head),
            Reports(lead, April, director),
            Reports(head, January, director));

        Assert.Empty(await ManagersOfAsync(person, January.AddDays(-1)));
        Assert.Equal([Active(lead), Active(head), Active(director)], await ManagersOfAsync(person, April.AddDays(-1)));
        Assert.Equal([Active(lead), Active(director)], await ManagersOfAsync(person, April));
    }

    [Fact]
    public async Task ManagersOfAsync_PositionsThatStartAfterToday_AreLeftOutToday()
    {
        var today = OrganisationApi.Today;
        var person = SignedIn("Kavya Iyer");
        var lead = SignedIn("Rohan Das");
        var nextLead = SignedIn("Nisha Menon");
        var head = SignedIn("Arjun Rao");
        await SaveAsync(
            person,
            lead,
            nextLead,
            head,
            Reports(person, today.AddDays(-30), lead),
            Reports(person, today.AddDays(30), nextLead),
            Reports(lead, today.AddDays(1), head));

        Assert.Equal([Active(lead)], await ManagersOfAsync(person, today));
    }

    [Fact]
    public async Task ManagersOfAsync_ManagersDeactivatedOrDeletedSince_ListsThemInactiveWithTheManagersAboveThem()
    {
        var person = SignedIn("Kavya Iyer");
        var lead = SignedIn("Rohan Das");
        var head = SignedIn("Nisha Menon");
        var director = SignedIn("Arjun Rao");
        await SaveAsync(
            person,
            lead,
            head,
            director,
            Reports(person, January, lead),
            Reports(lead, January, head),
            Reports(lead, July, director),
            Reports(head, January, director));
        await DeactivateAsync(lead);
        await DeleteAsync(head);

        Assert.Equal([Inactive(lead), Inactive(head), Active(director)], await ManagersOfAsync(person, April));
    }

    [Fact]
    public async Task ManagersOfAsync_ManagerWhoHasNeverSignedIn_ListsThemWithoutAnObjectId()
    {
        var person = SignedIn("Kavya Iyer");
        var lead = Registered("Rohan Das");
        var head = SignedIn("Nisha Menon");
        await SaveAsync(person, lead, head, Reports(person, January, lead), Reports(lead, January, head));

        Assert.Equal([new ReportingManagerEntry(lead.Id.Value, null, IsActive: true), Active(head)], await ManagersOfAsync(person, April));
    }

    [Fact]
    public async Task ManagersOfAsync_ReportingLinesThatLoop_ListEachManagerOnceAndNeverThePersonAskedAbout()
    {
        var person = SignedIn("Kavya Iyer");
        var lead = SignedIn("Rohan Das");
        var head = SignedIn("Nisha Menon");
        await SaveAsync(person, lead, head, Reports(person, January, lead), Reports(lead, January, head), Reports(head, January, person));

        Assert.Equal([Active(lead), Active(head)], await ManagersOfAsync(person, April));
        Assert.Equal([Active(head), Active(person)], await ManagersOfAsync(lead, April));
    }

    private static User SignedIn(string displayName) => SignedIn(displayName, Guid.CreateVersion7());

    private static User SignedIn(string displayName, Guid objectId) =>
        User.FirstSignIn(objectId, displayName, PeopleApi.UniqueWorkEmail(), DateTimeOffset.UnixEpoch).Value;

    private static User Registered(string displayName) => User.Register(displayName, PeopleApi.UniqueWorkEmail(), null, null, null, []).Value;

    private static Position Reports(User person, DateOnly from, User? manager) => Position.Record(person.Id, from, null, null, manager?.Id).Value;

    private static ReportingManagerEntry Active(User manager) => new(manager.Id.Value, manager.EntraObjectId, IsActive: true);

    private static ReportingManagerEntry Inactive(User manager) => new(manager.Id.Value, manager.EntraObjectId, IsActive: false);

    private Task<IReadOnlyList<ReportingManagerEntry>> ManagersOfAsync(User person, DateOnly on) => ManagersOfAsync(person.EntraObjectId!.Value, on);

    private async Task<IReadOnlyList<ReportingManagerEntry>> ManagersOfAsync(Guid objectId, DateOnly on)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<IReportingLines>().ManagersOfAsync(objectId, on, TestContext.Current.CancellationToken);
    }

    private async Task SaveAsync(params object[] entities)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        context.AddRange(entities);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task DeactivateAsync(User person)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        var record = await context.Users.SingleAsync(u => u.Id == person.Id, TestContext.Current.CancellationToken);
        record.Deactivate(DeactivationReason.AccountDisabled);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task DeleteAsync(User person)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        context.Users.Remove(await context.Users.SingleAsync(u => u.Id == person.Id, TestContext.Current.CancellationToken));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
