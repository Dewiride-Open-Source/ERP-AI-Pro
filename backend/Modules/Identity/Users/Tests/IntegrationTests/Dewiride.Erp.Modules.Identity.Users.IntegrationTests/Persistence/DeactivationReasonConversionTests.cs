using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.Modules.Identity.Users.People.Domain;
using Dewiride.Erp.Modules.Identity.Users.Persistence;
using Dewiride.Erp.Testing.Sql;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Dewiride.Erp.Modules.Identity.Users.IntegrationTests.Persistence;

// The test starts from a database the migrations before AddDeactivationReason built, so the records that migration gives a
// reason can be written before the column exists.
public sealed class DeactivationReasonConversionTests
{
    private const string BeforeConversion = "20261018072555_AddPersonThemes";

    private static readonly DateTimeOffset CreatedAt = new(2026, 5, 10, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Migrate_RecordsDeactivatedBeforeTheReasonExisted_AreDeactivatedByAnAdministratorAndActiveOnesHaveNoReason()
    {
        await using var database = await EmptyTestDatabase.CreateAsync();
        using var host = TestDatabaseHost.Build(database.ConnectionString);
        await using var scope = host.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(BeforeConversion, TestContext.Current.CancellationToken);
        var active = await InsertPersonAsync(context, UserStatus.Active, isDeleted: false);
        var deactivated = await InsertPersonAsync(context, UserStatus.Deactivated, isDeleted: false);
        var deletedDeactivated = await InsertPersonAsync(context, UserStatus.Deactivated, isDeleted: true);

        await migrator.MigrateAsync(cancellationToken: TestContext.Current.CancellationToken);

        var reasons = await context.Users.IgnoreQueryFilters().AsNoTracking()
            .ToDictionaryAsync(u => u.Id.Value, u => u.DeactivatedBecause, TestContext.Current.CancellationToken);
        Assert.Null(reasons[active]);
        Assert.Equal(DeactivationReason.Administrator, reasons[deactivated]);
        Assert.Equal(DeactivationReason.Administrator, reasons[deletedDeactivated]);
    }

    private static async Task<Guid> InsertPersonAsync(UsersDbContext context, UserStatus status, bool isDeleted)
    {
        var id = Guid.CreateVersion7();
        var workEmail = $"person-{id:N}@dewiride.test";
        var createdBy = ActorIds.System;
        var statusValue = (byte)status;
        await context.Database.ExecuteSqlAsync(
            $"INSERT INTO [identity_users].[Users] ([Id], [DisplayName], [WorkEmail], [Status], [Roles], [CreatedAt], [CreatedBy], [IsDeleted]) VALUES ({id}, N'Meera Nair', {workEmail}, {statusValue}, N'[]', {CreatedAt}, {createdBy}, {isDeleted})",
            TestContext.Current.CancellationToken);

        return id;
    }
}
