using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Persistence;

namespace Dewiride.Erp.BuildingBlocks.Numbering.Allocation;

// The command that issues a document asks for its number last, in its own unit of work, on its module's context, which declares
// `public DbSet<NumberSeries> NumberSeries => Set<NumberSeries>();` and `public DbSet<NumberSeriesYear> NumberSeriesYears =>
// Set<NumberSeriesYear>();`: the number commits with the document or not at all, and the documents of one kind are numbered one
// after the other until each commits.
public interface IDocumentNumbers
{
    Task<Result<DocumentNumber>> NextAsync(ModuleDbContext context, string documentType, DateOnly documentDate, CancellationToken cancellationToken);
}
