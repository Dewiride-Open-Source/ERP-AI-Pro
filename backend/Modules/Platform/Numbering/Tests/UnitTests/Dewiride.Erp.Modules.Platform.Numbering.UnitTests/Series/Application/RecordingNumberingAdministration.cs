using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.BuildingBlocks.Kernel.Time;
using Dewiride.Erp.BuildingBlocks.Numbering.Administration;
using Dewiride.Erp.BuildingBlocks.Numbering.Definitions;
using Dewiride.Erp.BuildingBlocks.Numbering.Domain;

namespace Dewiride.Erp.Modules.Platform.Numbering.UnitTests.Series.Application;

internal sealed class RecordingNumberingAdministration : INumberingAdministration
{
    public static readonly NumberedDocumentDetails Answer = new(
        new NumberedDocument("test.numbering.tax-invoices", "Test tax invoices", "Tax invoices the unit tests number.", "46(b)"),
        new FinancialYear(2026),
        null,
        null);

    public List<string> Calls { get; } = [];

    public NumberSeriesId? Id { get; private set; }

    public byte[]? Version { get; private set; }

    public string? DocumentType { get; private set; }

    public NumberFormat? Format { get; private set; }

    public long? NextNumber { get; private set; }

    public Task<IReadOnlyList<NumberedDocumentDetails>> ListAsync(CancellationToken cancellationToken)
    {
        Calls.Add(nameof(ListAsync));

        return Task.FromResult<IReadOnlyList<NumberedDocumentDetails>>([Answer]);
    }

    public Task<Result<NumberedDocumentDetails>> GetAsync(NumberSeriesId id, CancellationToken cancellationToken)
    {
        Calls.Add(nameof(GetAsync));
        Id = id;

        return Task.FromResult<Result<NumberedDocumentDetails>>(Answer);
    }

    public Task<Result<NumberedDocumentDetails>> SetUpAsync(string documentType, NumberFormat format, long nextNumber, CancellationToken cancellationToken)
    {
        Calls.Add(nameof(SetUpAsync));
        DocumentType = documentType;
        Format = format;
        NextNumber = nextNumber;

        return Task.FromResult<Result<NumberedDocumentDetails>>(Answer);
    }

    public Task<Result<NumberedDocumentDetails>> ChangeAsync(NumberSeriesId id, byte[] version, NumberFormat format, long? nextNumber, CancellationToken cancellationToken)
    {
        Calls.Add(nameof(ChangeAsync));
        Id = id;
        Version = version;
        Format = format;
        NextNumber = nextNumber;

        return Task.FromResult<Result<NumberedDocumentDetails>>(Answer);
    }
}
