using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.Modules.Identity.Users.People.Domain;
using Dewiride.Erp.Modules.Identity.Users.Persistence;
using Dewiride.Erp.Testing.Sql;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Dewiride.Erp.Modules.Identity.Users.IntegrationTests.Persistence;

public sealed class ThemePreferenceMigrationTests
{
    private const string BeforeThemes = "20261011123601_AddDepartmentsDesignationsAndPositions";

    [Fact]
    public async Task Migrate_PersonRecordedBeforeThemesExisted_FollowsTheThemeOfTheirDevice()
    {
        await using var database = await EmptyTestDatabase.CreateAsync();
        using var host = TestDatabaseHost.Build(database.ConnectionString);
        await using var scope = host.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(BeforeThemes, TestContext.Current.CancellationToken);
        var id = Guid.CreateVersion7();
        var workEmail = $"person-{id:N}@dewiride.test";
        var createdBy = ActorIds.System;
        var createdAt = new DateTimeOffset(2026, 10, 1, 4, 30, 0, TimeSpan.Zero);
        await context.Database.ExecuteSqlAsync(
            $"INSERT INTO [identity_users].[Users] ([Id], [DisplayName], [WorkEmail], [Status], [CreatedAt], [CreatedBy], [IsDeleted]) VALUES ({id}, N'Meera Nair', {workEmail}, CAST(1 AS tinyint), {createdAt}, {createdBy}, CAST(0 AS bit))",
            TestContext.Current.CancellationToken);

        await migrator.MigrateAsync(cancellationToken: TestContext.Current.CancellationToken);

        var user = await context.Users.AsNoTracking().SingleAsync(u => u.Id == UserId.From(id), TestContext.Current.CancellationToken);
        Assert.Equal(UserTheme.System, user.Theme);
    }
}
