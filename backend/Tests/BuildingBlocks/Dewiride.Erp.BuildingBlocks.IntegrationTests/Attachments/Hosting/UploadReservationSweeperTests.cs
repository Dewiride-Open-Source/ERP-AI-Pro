using System.Security.Cryptography;
using Dewiride.Erp.BuildingBlocks.Attachments;
using Dewiride.Erp.BuildingBlocks.Attachments.Domain;
using Dewiride.Erp.BuildingBlocks.Attachments.Hosting;
using Dewiride.Erp.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Attachments.Hosting;

// A sweep removes every expired reservation in the per-process test database, not only its own test's. These reservations are
// dated in 2001, so no sweep here can expire an upload running in a parallel test class, and each test has its own day; the
// fresh reservation, the only one meant to outlive its test, has the latest day, beyond every other test's cutoff.
public sealed class UploadReservationSweeperTests(ErpApiFactory factory) : IClassFixture<ErpApiFactory>
{
    private static readonly DateTimeOffset UncommittedDay = new(2001, 3, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset CommittedDay = new(2001, 3, 2, 0, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset ReferencedDay = new(2001, 3, 3, 0, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset FreshDay = new(2001, 3, 4, 0, 0, 0, TimeSpan.Zero);

    private TimeSpan Lifetime => factory.Services.GetRequiredService<IOptions<AttachmentsOptions>>().Value.UploadReservationLifetime;

    [Fact]
    public async Task SweepOnceAsync_ExpiredReservationWithUncommittedBlocks_RemovesTheReservationAndTheBlocks()
    {
        var contentId = await ReserveAsync(UncommittedDay);
        await StoredBlobs.StageBlockAsync(contentId, RandomNumberGenerator.GetBytes(1_024));
        Assert.NotNull(await StoredBlobs.FindAnyAsync(contentId));

        var removed = await SweepAtAsync(UncommittedDay + Lifetime + TimeSpan.FromMinutes(1));

        Assert.Equal(1, removed);
        Assert.False(await IsReservedAsync(contentId));
        Assert.Null(await StoredBlobs.FindAnyAsync(contentId));
    }

    [Fact]
    public async Task SweepOnceAsync_ExpiredReservationWithACommittedBlobButNoStoredContent_RemovesTheReservationAndTheBlob()
    {
        var contentId = await ReserveAsync(CommittedDay);
        await StoredBlobs.ReplaceAsync(contentId, RandomNumberGenerator.GetBytes(1_024));
        Assert.True(await StoredBlobs.IsCommittedAsync(contentId));

        var removed = await SweepAtAsync(CommittedDay + Lifetime + TimeSpan.FromMinutes(1));

        Assert.Equal(1, removed);
        Assert.False(await IsReservedAsync(contentId));
        Assert.Null(await StoredBlobs.FindAnyAsync(contentId));
    }

    [Fact]
    public async Task SweepOnceAsync_ExpiredReservationWhoseContentIsStored_RemovesOnlyTheReservation()
    {
        var content = TestFiles.Pdf(5_000);
        var details = await factory.UploadAsync($"{TestFiles.Stamp()}-agreement.pdf", TestFiles.PdfType, content);
        var contentId = await factory.ContentIdOfAsync(details.Id);
        await ReserveAsync(ReferencedDay, contentId);

        var removed = await SweepAtAsync(ReferencedDay + Lifetime + TimeSpan.FromMinutes(1));

        Assert.Equal(0, removed);
        Assert.False(await IsReservedAsync(contentId));
        Assert.True(await StoredBlobs.IsCommittedAsync(contentId));
        Assert.Equal(content, await factory.DownloadAsync(details.Id));
    }

    [Fact]
    public async Task SweepOnceAsync_ReservationExactlyAtTheLifetime_KeepsTheReservationAndItsBlocks()
    {
        var contentId = await ReserveAsync(FreshDay);
        await StoredBlobs.StageBlockAsync(contentId, RandomNumberGenerator.GetBytes(1_024));

        var removed = await SweepAtAsync(FreshDay + Lifetime);

        Assert.Equal(0, removed);
        Assert.True(await IsReservedAsync(contentId));
        Assert.NotNull(await StoredBlobs.FindAnyAsync(contentId));
    }

    private async Task<StoredContentId> ReserveAsync(DateTimeOffset reservedAt, StoredContentId? contentId = null)
    {
        var id = contentId ?? StoredContentId.Create();
        await factory.QueryAsync(async (context, cancellationToken) =>
        {
            context.UploadReservations.Add(new UploadReservation(id, reservedAt, Guid.CreateVersion7()));

            return await context.SaveChangesAsync(cancellationToken);
        });

        return id;
    }

    private Task<bool> IsReservedAsync(StoredContentId contentId) =>
        factory.QueryAsync((context, cancellationToken) => context.UploadReservations.AnyAsync(r => r.ContentId == contentId, cancellationToken));

    private async Task<int> SweepAtAsync(DateTimeOffset now)
    {
        using var sweeper = ActivatorUtilities.CreateInstance<UploadReservationSweeper>(factory.Services, new FakeTimeProvider(now));

        return await sweeper.SweepOnceAsync(TestContext.Current.CancellationToken);
    }
}
