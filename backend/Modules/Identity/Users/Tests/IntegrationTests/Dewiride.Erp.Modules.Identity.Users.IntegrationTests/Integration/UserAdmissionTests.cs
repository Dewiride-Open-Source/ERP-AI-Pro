using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.Modules.Identity.Users.IntegrationTests.People.Endpoints;
using Dewiride.Erp.Modules.Identity.Users.People.Domain;
using Dewiride.Erp.Modules.Identity.Users.Persistence;
using Dewiride.Erp.Testing;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.Modules.Identity.Users.IntegrationTests.Integration;

public sealed class UserAdmissionTests(ErpApiFactory factory) : IClassFixture<ErpApiFactory>
{
    [Fact]
    public async Task AdmitAsync_FirstSignIn_CreatesAnActiveRecordLinkedToTheAccount()
    {
        var person = new SignedInPerson(Guid.CreateVersion7(), "Meera Nair", PeopleApi.UniqueWorkEmail().ToUpperInvariant());

        Assert.True(await AdmitAsync(person));

        var user = await RecordOfAsync(person.ObjectId);
        Assert.NotNull(user);
        Assert.Equal("Meera Nair", user.DisplayName);
        Assert.Equal(person.UserName.ToLowerInvariant(), user.WorkEmail);
        Assert.True(user.IsActive);
        Assert.NotNull(user.LastSignedInAt);
    }

    [Fact]
    public async Task AdmitAsync_LaterSignIn_BringsTheNameAndEmailUpToDateOnTheSameRecord()
    {
        var objectId = Guid.CreateVersion7();
        Assert.True(await AdmitAsync(new SignedInPerson(objectId, "Meera", PeopleApi.UniqueWorkEmail())));
        var first = await RecordOfAsync(objectId);
        var workEmail = PeopleApi.UniqueWorkEmail();

        Assert.True(await AdmitAsync(new SignedInPerson(objectId, "Meera Nair", workEmail)));

        var later = await RecordOfAsync(objectId);
        Assert.NotNull(first);
        Assert.NotNull(later);
        Assert.Equal(first.Id, later.Id);
        Assert.Equal("Meera Nair", later.DisplayName);
        Assert.Equal(workEmail, later.WorkEmail);
    }

    [Fact]
    public async Task AdmitAsync_RecordRegisteredWithTheWorkEmail_LinksItInsteadOfCreatingAnother()
    {
        var workEmail = PeopleApi.UniqueWorkEmail();
        var registered = await RegisterAsync(User.Register(null, "Meera", workEmail, "DW-0042", null, null, null).Value);
        var objectId = Guid.CreateVersion7();

        Assert.True(await AdmitAsync(new SignedInPerson(objectId, "Meera Nair", workEmail.ToUpperInvariant())));

        var linked = await RecordOfAsync(objectId);
        Assert.NotNull(linked);
        Assert.Equal(registered, linked.Id);
        Assert.Equal("Meera Nair", linked.DisplayName);
        Assert.Equal("DW-0042", linked.EmployeeCode);
    }

    [Fact]
    public async Task AdmitAsync_RecordRegisteredWithTheObjectId_LinksItWhateverItsEmail()
    {
        var objectId = Guid.CreateVersion7();
        var registered = await RegisterAsync(User.Register(objectId, "Meera", PeopleApi.UniqueWorkEmail(), null, null, null, null).Value);
        var workEmail = PeopleApi.UniqueWorkEmail();

        Assert.True(await AdmitAsync(new SignedInPerson(objectId, "Meera Nair", workEmail)));

        var linked = await RecordOfAsync(objectId);
        Assert.NotNull(linked);
        Assert.Equal(registered, linked.Id);
        Assert.Equal(workEmail, linked.WorkEmail);
    }

    [Fact]
    public async Task AdmitAsync_AnotherAccountWithTheWorkEmailOfALinkedRecord_StartsARecordOfItsOwn()
    {
        var workEmail = PeopleApi.UniqueWorkEmail();
        var leaver = Guid.CreateVersion7();
        var joiner = Guid.CreateVersion7();
        Assert.True(await AdmitAsync(new SignedInPerson(leaver, "Meera Nair", workEmail)));

        Assert.True(await AdmitAsync(new SignedInPerson(joiner, "Meera Iyer", workEmail)));

        var first = await RecordOfAsync(leaver);
        var second = await RecordOfAsync(joiner);
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(workEmail, second.WorkEmail);
    }

    [Fact]
    public async Task AdmitAsync_DeactivatedRecord_RefusesAndLeavesTheRecordAsItWas()
    {
        var objectId = Guid.CreateVersion7();
        var user = User.FirstSignIn(objectId, "Meera Nair", PeopleApi.UniqueWorkEmail(), DateTimeOffset.UnixEpoch).Value;
        user.Deactivate();
        await RegisterAsync(user);

        Assert.False(await AdmitAsync(new SignedInPerson(objectId, "Someone Else", PeopleApi.UniqueWorkEmail())));

        var record = await RecordOfAsync(objectId);
        Assert.NotNull(record);
        Assert.Equal("Meera Nair", record.DisplayName);
        Assert.Equal(DateTimeOffset.UnixEpoch, record.LastSignedInAt);
    }

    [Fact]
    public async Task AdmitAsync_DeletedRecord_StartsANewRecord()
    {
        var objectId = Guid.CreateVersion7();
        var deleted = await RegisterAsync(User.FirstSignIn(objectId, "Meera Nair", PeopleApi.UniqueWorkEmail(), DateTimeOffset.UnixEpoch).Value);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
            context.Users.Remove(await context.Users.SingleAsync(u => u.Id == deleted, TestContext.Current.CancellationToken));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        Assert.True(await AdmitAsync(new SignedInPerson(objectId, "Meera Nair", PeopleApi.UniqueWorkEmail())));

        var record = await RecordOfAsync(objectId);
        Assert.NotNull(record);
        Assert.NotEqual(deleted, record.Id);
    }

    [Fact]
    public async Task AdmitAsync_MoreFirstSignInsOfOnePersonAtOnceThanAttempts_AdmitsEveryOneOnOneRecord()
    {
        var person = new SignedInPerson(Guid.CreateVersion7(), "Meera Nair", PeopleApi.UniqueWorkEmail());

        var admitted = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => AdmitAsync(person), TestContext.Current.CancellationToken)));

        Assert.All(admitted, Assert.True);
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<UsersDbContext>().Users.CountAsync(u => u.EntraObjectId == person.ObjectId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AdmitAsync_ClaimsWithoutAnEmail_Throws()
    {
        var person = new SignedInPerson(Guid.CreateVersion7(), "Meera Nair", string.Empty);

        await Assert.ThrowsAsync<InvalidOperationException>(() => AdmitAsync(person));
        Assert.Null(await RecordOfAsync(person.ObjectId));
    }

    [Fact]
    public async Task IsAdmittedAsync_ActiveRecord_ReturnsTrue()
    {
        var objectId = Guid.CreateVersion7();
        await RegisterAsync(User.FirstSignIn(objectId, "Meera Nair", PeopleApi.UniqueWorkEmail(), DateTimeOffset.UnixEpoch).Value);

        Assert.True(await IsAdmittedAsync(objectId));
    }

    [Fact]
    public async Task IsAdmittedAsync_DeactivatedRecord_ReturnsFalse()
    {
        var objectId = Guid.CreateVersion7();
        var user = User.FirstSignIn(objectId, "Meera Nair", PeopleApi.UniqueWorkEmail(), DateTimeOffset.UnixEpoch).Value;
        user.Deactivate();
        await RegisterAsync(user);

        Assert.False(await IsAdmittedAsync(objectId));
    }

    [Fact]
    public async Task IsAdmittedAsync_NoRecord_ReturnsFalse()
    {
        Assert.False(await IsAdmittedAsync(Guid.CreateVersion7()));
    }

    private async Task<bool> AdmitAsync(SignedInPerson person)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<IPersonAdmission>().AdmitAsync(person, TestContext.Current.CancellationToken);
    }

    private async Task<bool> IsAdmittedAsync(Guid objectId)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<IPersonAdmission>().IsAdmittedAsync(objectId, TestContext.Current.CancellationToken);
    }

    private async Task<UserId> RegisterAsync(User user)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        context.Users.Add(user);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return user.Id;
    }

    private async Task<User?> RecordOfAsync(Guid objectId)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<UsersDbContext>().Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.EntraObjectId == objectId, TestContext.Current.CancellationToken);
    }
}
