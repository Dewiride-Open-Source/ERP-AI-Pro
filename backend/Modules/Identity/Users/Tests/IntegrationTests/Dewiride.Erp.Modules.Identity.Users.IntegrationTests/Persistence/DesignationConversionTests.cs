using Dewiride.Erp.BuildingBlocks.Application.Actors;
using Dewiride.Erp.BuildingBlocks.Kernel.Time;
using Dewiride.Erp.Modules.Identity.Users.Persistence;
using Dewiride.Erp.Testing.Sql;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Dewiride.Erp.Modules.Identity.Users.IntegrationTests.Persistence;

// Each test starts from a database the migrations before AddDepartmentsDesignationsAndPositions built, so the rows that
// migration converts can be written while the free-text column still exists.
public sealed class DesignationConversionTests
{
    private const string BeforeConversion = "20261011071223_AddPersonRoles";

    private static readonly DateTimeOffset CreatedAt = new(2026, 5, 10, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Migrate_FreeTextDesignations_BecomeDesignationsAndThePositionsOfThePeopleWhoHadThem()
    {
        await using var database = await EmptyTestDatabase.CreateAsync();
        using var host = TestDatabaseHost.Build(database.ConnectionString);
        await using var scope = host.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(BeforeConversion, TestContext.Current.CancellationToken);
        var joined = await InsertPersonAsync(context, "Accountant", new DateOnly(2026, 4, 1), isDeleted: false);
        var variant = await InsertPersonAsync(context, " accountant ", null, isDeleted: false);
        var joiningLater = await InsertPersonAsync(context, "Engineer", new DateOnly(2099, 1, 1), isDeleted: false);
        var blank = await InsertPersonAsync(context, " ", null, isDeleted: false);
        var none = await InsertPersonAsync(context, null, null, isDeleted: false);
        var deleted = await InsertPersonAsync(context, "Analyst", new DateOnly(2025, 1, 1), isDeleted: true);
        var deletedEngineer = await InsertPersonAsync(context, "Engineer", new DateOnly(2025, 6, 1), isDeleted: true);

        await migrator.MigrateAsync(cancellationToken: TestContext.Current.CancellationToken);

        var designations = await context.Designations.IgnoreQueryFilters().AsNoTracking().ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(["Accountant", "Analyst", "Engineer"], designations.Select(d => d.Name).Order(StringComparer.Ordinal));
        Assert.All(designations, designation => Assert.Equal(ActorIds.System, designation.CreatedBy));
        Assert.Equal(["Analyst"], designations.Where(d => d.IsDeleted).Select(d => d.Name));
        var onlyTheDeletedHeld = designations.Single(d => d.Name == "Analyst");
        Assert.Equal(ActorIds.System, onlyTheDeletedHeld.DeletedBy);
        Assert.NotNull(onlyTheDeletedHeld.DeletedAt);
        var named = designations.ToDictionary(d => d.Name, d => (Guid?)d.Id.Value);
        var positions = await context.Positions.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(5, positions.Count);
        Assert.All(positions, position =>
        {
            Assert.Null(position.DepartmentId);
            Assert.Null(position.ManagerId);
        });
        var held = positions.ToDictionary(p => p.UserId.Value, p => (p.EffectiveFrom, p.DesignationId?.Value));
        Assert.Equal((new DateOnly(2026, 4, 1), named["Accountant"]), held[joined]);
        Assert.Equal((new DateOnly(2026, 5, 11), named["Accountant"]), held[variant]);
        Assert.Equal((IndianStandardTime.Today(TimeProvider.System), named["Engineer"]), held[joiningLater]);
        Assert.Equal((new DateOnly(2025, 1, 1), named["Analyst"]), held[deleted]);
        Assert.Equal((new DateOnly(2025, 6, 1), named["Engineer"]), held[deletedEngineer]);
        Assert.DoesNotContain(blank, held.Keys);
        Assert.DoesNotContain(none, held.Keys);
        Assert.False(await HasDesignationColumnAsync(context));
    }

    [Fact]
    public async Task MigrateBack_BeforeTheConversion_RestoresEachPersonsCurrentDesignationAsText()
    {
        await using var database = await EmptyTestDatabase.CreateAsync();
        using var host = TestDatabaseHost.Build(database.ConnectionString);
        await using var scope = host.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(BeforeConversion, TestContext.Current.CancellationToken);
        var accountant = await InsertPersonAsync(context, "Accountant", new DateOnly(2026, 4, 1), isDeleted: false);
        var movingOn = await InsertPersonAsync(context, "Accountant", new DateOnly(2026, 4, 1), isDeleted: false);
        var moved = await InsertPersonAsync(context, "Accountant", new DateOnly(2026, 4, 1), isDeleted: false);
        var none = await InsertPersonAsync(context, null, null, isDeleted: false);
        await migrator.MigrateAsync(cancellationToken: TestContext.Current.CancellationToken);
        var today = IndianStandardTime.Today(TimeProvider.System);
        await InsertEmptyPositionAsync(context, movingOn, today.AddDays(30));
        await InsertEmptyPositionAsync(context, moved, today.AddDays(-1));

        await migrator.MigrateAsync(BeforeConversion, TestContext.Current.CancellationToken);

        Assert.True(await HasDesignationColumnAsync(context));
        Assert.Equal("Accountant", await DesignationTextAsync(context, accountant));
        Assert.Equal("Accountant", await DesignationTextAsync(context, movingOn));
        Assert.Null(await DesignationTextAsync(context, moved));
        Assert.Null(await DesignationTextAsync(context, none));
    }

    private static async Task<Guid> InsertPersonAsync(UsersDbContext context, string? designation, DateOnly? dateOfJoining, bool isDeleted)
    {
        var id = Guid.CreateVersion7();
        var workEmail = $"person-{id:N}@dewiride.test";
        var createdBy = ActorIds.System;
        await context.Database.ExecuteSqlAsync(
            $"INSERT INTO [identity_users].[Users] ([Id], [DisplayName], [WorkEmail], [Designation], [DateOfJoining], [Status], [CreatedAt], [CreatedBy], [IsDeleted]) VALUES ({id}, N'Meera Nair', {workEmail}, {designation}, {dateOfJoining}, CAST(1 AS tinyint), {CreatedAt}, {createdBy}, {isDeleted})",
            TestContext.Current.CancellationToken);

        return id;
    }

    private static async Task InsertEmptyPositionAsync(UsersDbContext context, Guid person, DateOnly effectiveFrom)
    {
        var id = Guid.CreateVersion7();
        var createdBy = ActorIds.System;
        await context.Database.ExecuteSqlAsync(
            $"INSERT INTO [identity_users].[Positions] ([Id], [UserId], [EffectiveFrom], [CreatedAt], [CreatedBy]) VALUES ({id}, {person}, {effectiveFrom}, {CreatedAt}, {createdBy})",
            TestContext.Current.CancellationToken);
    }

    private static async Task<bool> HasDesignationColumnAsync(UsersDbContext context) =>
        await context.Database
            .SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM sys.columns WHERE [object_id] = OBJECT_ID(N'[identity_users].[Users]') AND [name] = N'Designation'")
            .SingleAsync(TestContext.Current.CancellationToken) == 1;

    private static Task<string?> DesignationTextAsync(UsersDbContext context, Guid id) =>
        context.Database
            .SqlQuery<string?>($"SELECT [Designation] AS [Value] FROM [identity_users].[Users] WHERE [Id] = {id}")
            .SingleAsync(TestContext.Current.CancellationToken);
}
