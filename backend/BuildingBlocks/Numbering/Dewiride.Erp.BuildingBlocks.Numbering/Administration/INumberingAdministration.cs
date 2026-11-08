using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Numbering.Domain;

namespace Dewiride.Erp.BuildingBlocks.Numbering.Administration;

// Setting a numbering up opens the current financial year at its next number. A change applies to the current year while it has
// given no number, and its next number may change only then; a year that has given one keeps its format, which a change makes
// the next year's.
public interface INumberingAdministration
{
    Task<IReadOnlyList<NumberedDocumentDetails>> ListAsync(CancellationToken cancellationToken);

    Task<Result<NumberedDocumentDetails>> GetAsync(NumberSeriesId id, CancellationToken cancellationToken);

    Task<Result<NumberedDocumentDetails>> SetUpAsync(string documentType, NumberFormat format, long nextNumber, CancellationToken cancellationToken);

    Task<Result<NumberedDocumentDetails>> ChangeAsync(NumberSeriesId id, byte[] version, NumberFormat format, long? nextNumber, CancellationToken cancellationToken);
}
