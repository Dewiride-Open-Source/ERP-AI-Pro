using System.Net;
using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.Modules.Identity.Users.Contracts.People;
using Dewiride.Erp.Modules.Identity.Users.People.Domain;
using Dewiride.Erp.Modules.Identity.Users.People.Endpoints.Responses;
using Dewiride.Erp.Modules.Identity.Users.Persistence;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Identity.Users.IntegrationTests.People.Endpoints;

// The test header runs no admission, so both administrators' requests reach the handlers however the other one ends, as two
// requests do whose sessions both passed the admission check before either change was saved.
public sealed class ActingAdministratorTests(ErpApiFactory factory) : IClassFixture<ErpApiFactory>
{
    [Fact]
    public async Task ChangeStatus_TwoAdministratorsDeactivatingEachOtherAtOnce_LeavesOneActiveAndRefusesTheSecondAsActorInactive()
    {
        var (first, firstRecord) = await NewAdministratorAsync();
        var (second, secondRecord) = await NewAdministratorAsync();
        using var firstClient = factory.CreateClient().AsUser(first);
        using var secondClient = factory.CreateClient().AsUser(second);

        var responses = await Task.WhenAll(
            DeactivateAsync(firstClient, secondRecord),
            DeactivateAsync(secondClient, firstRecord));

        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
            var refused = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
            Assert.Equal("user.actor-inactive", PeopleApi.CodeOf(await PeopleApi.ReadProblemAsync(refused)));
            Assert.Single(await StatusesOfAsync(first, second), status => status == UserStatus.Active);
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    [Fact]
    public async Task Delete_TwoAdministratorsDeletingEachOtherAtOnce_KeepsOneAndRefusesTheSecondAsActorInactive()
    {
        var (first, firstRecord) = await NewAdministratorAsync();
        var (second, secondRecord) = await NewAdministratorAsync();
        using var firstClient = factory.CreateClient().AsUser(first);
        using var secondClient = factory.CreateClient().AsUser(second);

        var responses = await Task.WhenAll(
            firstClient.DeleteAsync(PeopleApi.Path($"/{secondRecord.Id}"), TestContext.Current.CancellationToken),
            secondClient.DeleteAsync(PeopleApi.Path($"/{firstRecord.Id}"), TestContext.Current.CancellationToken));

        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.NoContent);
            var refused = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
            Assert.Equal("user.actor-inactive", PeopleApi.CodeOf(await PeopleApi.ReadProblemAsync(refused)));
            await using var scope = factory.Services.CreateAsyncScope();
            var kept = await scope.ServiceProvider.GetRequiredService<UsersDbContext>().Users
                .CountAsync(u => u.EntraObjectId == first.ObjectId || u.EntraObjectId == second.ObjectId, TestContext.Current.CancellationToken);
            Assert.Equal(1, kept);
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    [Fact]
    public async Task ChangeStatus_ByAnAdministratorDeactivatedBeforehand_Returns409ActorInactiveAndChangesNothing()
    {
        var (administrator, record) = await NewAdministratorAsync();
        using var other = factory.CreateClient().AsUser(TestUsers.Administrator);
        using (var deactivated = await DeactivateAsync(other, record))
        {
            Assert.Equal(HttpStatusCode.OK, deactivated.StatusCode);
        }

        var person = await PeopleApi.RegisterAsync(other, PeopleApi.Person(PeopleApi.UniqueWorkEmail()));
        using var client = factory.CreateClient().AsUser(administrator);

        using var response = await DeactivateAsync(client, person);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("user.actor-inactive", PeopleApi.CodeOf(await PeopleApi.ReadProblemAsync(response)));
        using var read = await other.GetAsync(PeopleApi.Path($"/{person.Id}"), TestContext.Current.CancellationToken);
        Assert.Equal(PersonStatus.Active, (await PeopleApi.ReadPersonAsync(read)).Status);
    }

    [Fact]
    public async Task ChangeStatus_ByAnAdministratorWhoseDeletedRecordANewerActiveOneReplaced_IsAllowed()
    {
        var (administrator, record) = await NewAdministratorAsync();
        using var other = factory.CreateClient().AsUser(TestUsers.Administrator);
        using (var deleted = await other.DeleteAsync(PeopleApi.Path($"/{record.Id}"), TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        }

        await AddRecordAsync(administrator);
        var person = await PeopleApi.RegisterAsync(other, PeopleApi.Person(PeopleApi.UniqueWorkEmail()));
        using var client = factory.CreateClient().AsUser(administrator);

        using var response = await DeactivateAsync(client, person);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ByAnAdministratorDeletedBeforehand_Returns409ActorInactiveAndKeepsTheRecord()
    {
        var (administrator, record) = await NewAdministratorAsync();
        using var other = factory.CreateClient().AsUser(TestUsers.Administrator);
        using (var deleted = await other.DeleteAsync(PeopleApi.Path($"/{record.Id}"), TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        }

        var person = await PeopleApi.RegisterAsync(other, PeopleApi.Person(PeopleApi.UniqueWorkEmail()));
        using var client = factory.CreateClient().AsUser(administrator);

        using var response = await client.DeleteAsync(PeopleApi.Path($"/{person.Id}"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("user.actor-inactive", PeopleApi.CodeOf(await PeopleApi.ReadProblemAsync(response)));
        using var read = await other.GetAsync(PeopleApi.Path($"/{person.Id}"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
    }

    private static Task<HttpResponseMessage> DeactivateAsync(HttpClient client, PersonResponse person) =>
        client.PutAsJsonAsync(PeopleApi.Path($"/{person.Id}/status"), new { status = "deactivated", version = person.Version }, PeopleApi.Json, TestContext.Current.CancellationToken);

    private async Task<(TestUser Administrator, PersonResponse Record)> NewAdministratorAsync()
    {
        var objectId = Guid.CreateVersion7();
        var administrator = new TestUser(objectId, "Vikram Iyer", $"vikram.iyer.{objectId:N}@dewiride.test", [AppRoles.User, AppRoles.Administrator]);
        factory.Services.GetRequiredService<TestTokenEndpoint>().Admit(administrator);
        var id = await AddRecordAsync(administrator);
        using var client = factory.CreateClient().AsUser(TestUsers.Administrator);
        using var read = await client.GetAsync(PeopleApi.Path($"/{id}"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);

        return (administrator, await PeopleApi.ReadPersonAsync(read));
    }

    private async Task<Guid> AddRecordAsync(TestUser person)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        var record = User.Invite(person.ObjectId, person.Name, person.UserName, null, null, null, []).Value;
        context.Users.Add(record);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return record.Id.Value;
    }

    private async Task<List<UserStatus>> StatusesOfAsync(params TestUser[] people)
    {
        List<Guid?> objectIds = [.. people.Select(person => (Guid?)person.ObjectId)];
        await using var scope = factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<UsersDbContext>().Users
            .Where(u => objectIds.Contains(u.EntraObjectId))
            .Select(u => u.Status)
            .ToListAsync(TestContext.Current.CancellationToken);
    }
}
