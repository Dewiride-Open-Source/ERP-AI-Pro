using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;
using Dewiride.Erp.Modules.Identity.Users.Departments.Domain;
using Dewiride.Erp.Modules.Identity.Users.Designations.Domain;
using Dewiride.Erp.Modules.Identity.Users.Integration;
using Dewiride.Erp.Modules.Identity.Users.IntegrationTests.People.Endpoints;
using Dewiride.Erp.Modules.Identity.Users.IntegrationTests.Shared;
using Dewiride.Erp.Modules.Identity.Users.People.Domain;
using Dewiride.Erp.Modules.Identity.Users.Persistence;
using Dewiride.Erp.Testing;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Identity.Users.IntegrationTests.Integration;

public sealed class UserRecordNamesTests(ErpApiFactory factory) : IClassFixture<ErpApiFactory>
{
    [Fact]
    public async Task EntityTypes_Always_AreThePeopleTheDepartmentsAndTheDesignations()
    {
        await using var scope = factory.Services.CreateAsyncScope();

        var names = Names(scope.ServiceProvider);

        Assert.Equal(
            [ChangeRecordsApi.DepartmentsKey, ChangeRecordsApi.DesignationsKey, ChangeRecordsApi.PeopleKey],
            names.EntityTypes.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task NamesAsync_People_NamesEachRecordDeletedOnesIncludedAndLeavesUnknownIdsOut()
    {
        var kept = await AddAsync(User.Register("Meera Nair", PeopleApi.UniqueWorkEmail(), null, null, null, []).Value);
        var deleted = await AddAsync(User.Register("Ravi Kumar", PeopleApi.UniqueWorkEmail(), null, null, null, []).Value);
        await RemoveAsync(context => context.Users.SingleAsync(u => u.Id == deleted.Id, TestContext.Current.CancellationToken));

        var names = await NamesAsync(ChangeRecordsApi.PeopleKey, kept.Id.Value, deleted.Id.Value, Guid.CreateVersion7());

        AssertNamed(names, (kept.Id.Value, "Meera Nair"), (deleted.Id.Value, "Ravi Kumar"));
    }

    [Fact]
    public async Task NamesAsync_Departments_NamesEachDepartmentDeletedOnesIncluded()
    {
        var kept = await AddAsync(Department.Create(OrganisationApi.UniqueName("Finance"), null).Value);
        var deleted = await AddAsync(Department.Create(OrganisationApi.UniqueName("Archive"), null).Value);
        await RemoveAsync(context => context.Departments.SingleAsync(d => d.Id == deleted.Id, TestContext.Current.CancellationToken));

        var names = await NamesAsync(ChangeRecordsApi.DepartmentsKey, kept.Id.Value, deleted.Id.Value, Guid.CreateVersion7());

        AssertNamed(names, (kept.Id.Value, kept.Name), (deleted.Id.Value, deleted.Name));
    }

    [Fact]
    public async Task NamesAsync_Designations_NamesEachDesignationDeletedOnesIncluded()
    {
        var kept = await AddAsync(Designation.Create(OrganisationApi.UniqueName("Analyst")).Value);
        var deleted = await AddAsync(Designation.Create(OrganisationApi.UniqueName("Intern")).Value);
        await RemoveAsync(context => context.Designations.SingleAsync(d => d.Id == deleted.Id, TestContext.Current.CancellationToken));

        var names = await NamesAsync(ChangeRecordsApi.DesignationsKey, kept.Id.Value, deleted.Id.Value, Guid.CreateVersion7());

        AssertNamed(names, (kept.Id.Value, kept.Name), (deleted.Id.Value, deleted.Name));
    }

    [Fact]
    public async Task NamesAsync_ChangeKeyItDoesNotName_Throws()
    {
        await using var scope = factory.Services.CreateAsyncScope();

        await Assert.ThrowsAsync<ArgumentException>(() => Names(scope.ServiceProvider).NamesAsync(ChangeRecordsApi.PositionsKey, [Guid.CreateVersion7()], TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DisplayNamesAsync_ObjectIdOfAnOlderRecordAndANewerDeletedOne_NamesThePersonByTheRecordNotDeleted()
    {
        var objectId = Guid.CreateVersion7();
        var older = await AddAsync(User.FirstSignIn(objectId, "Meera Nair", PeopleApi.UniqueWorkEmail(), DateTimeOffset.UnixEpoch).Value);
        await RemoveAsync(context => context.Users.SingleAsync(u => u.Id == older.Id, TestContext.Current.CancellationToken));
        var newer = await AddAsync(User.FirstSignIn(objectId, "Meera Iyer", PeopleApi.UniqueWorkEmail(), DateTimeOffset.UnixEpoch).Value);
        await RemoveAsync(context => context.Users.SingleAsync(u => u.Id == newer.Id, TestContext.Current.CancellationToken));
        await RestoreAsync(older.Id);

        var names = await DisplayNamesAsync(objectId);

        AssertNamed(names, (objectId, "Meera Nair"));
    }

    [Fact]
    public async Task DisplayNamesAsync_ObjectIdOfTwoDeletedRecords_NamesThePersonByTheNewerRecord()
    {
        var objectId = Guid.CreateVersion7();
        var older = await AddAsync(User.FirstSignIn(objectId, "Meera Nair", PeopleApi.UniqueWorkEmail(), DateTimeOffset.UnixEpoch).Value);
        await RemoveAsync(context => context.Users.SingleAsync(u => u.Id == older.Id, TestContext.Current.CancellationToken));
        var newer = await AddAsync(User.FirstSignIn(objectId, "Meera Iyer", PeopleApi.UniqueWorkEmail(), DateTimeOffset.UnixEpoch).Value);
        await RemoveAsync(context => context.Users.SingleAsync(u => u.Id == newer.Id, TestContext.Current.CancellationToken));

        var names = await DisplayNamesAsync(objectId, Guid.CreateVersion7());

        AssertNamed(names, (objectId, "Meera Iyer"));
    }

    private static UserRecordNames Names(IServiceProvider services) => Assert.Single(services.GetServices<IRecordNames>().OfType<UserRecordNames>());

    private static void AssertNamed(IReadOnlyDictionary<Guid, string> names, params (Guid Id, string Name)[] expected) =>
        Assert.Equal(expected.OrderBy(pair => pair.Id), names.Select(pair => (pair.Key, pair.Value)).OrderBy(pair => pair.Key));

    private async Task<IReadOnlyDictionary<Guid, string>> NamesAsync(string entityType, params Guid[] ids)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        return await Names(scope.ServiceProvider).NamesAsync(entityType, ids, TestContext.Current.CancellationToken);
    }

    private async Task<IReadOnlyDictionary<Guid, string>> DisplayNamesAsync(params Guid[] objectIds)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var people = Assert.Single(scope.ServiceProvider.GetServices<IPersonNames>().OfType<UserRecordNames>());

        return await people.DisplayNamesAsync(objectIds, TestContext.Current.CancellationToken);
    }

    private async Task<TEntity> AddAsync<TEntity>(TEntity entity)
        where TEntity : class
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        context.Add(entity);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return entity;
    }

    private async Task RemoveAsync<TEntity>(Func<UsersDbContext, Task<TEntity>> find)
        where TEntity : class
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        context.Remove(await find(context));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task RestoreAsync(UserId id)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        var user = await context.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == id, TestContext.Current.CancellationToken);
        context.Entry(user).Property(u => u.IsDeleted).CurrentValue = false;
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
