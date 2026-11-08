using Dewiride.Erp.BuildingBlocks.Kernel.Domain;
using Dewiride.Erp.BuildingBlocks.Numbering.Allocation;

namespace Dewiride.Erp.BuildingBlocks.IntegrationTests.Numbering.Sample;

// A document numbered in the transaction that saves it, as a module's command numbers what it issues; its number never changes.
public sealed class SampleInvoice : IAppendOnly
{
    public const int DocumentTypeMaxLength = 128;

    public const int NumberMaxLength = 64;

    private SampleInvoice(SampleInvoiceId id)
    {
        Id = id;
    }

    public SampleInvoiceId Id { get; private set; }

    public string DocumentType { get; private set; } = string.Empty;

    public string Number { get; private set; } = string.Empty;

    public string Year { get; private set; } = string.Empty;

    public long Sequence { get; private set; }

    public static SampleInvoice Issue(string documentType, DocumentNumber number)
    {
        ArgumentNullException.ThrowIfNull(number);

        return new SampleInvoice(SampleInvoiceId.Create()) { DocumentType = documentType, Number = number.Text, Year = number.Year.Label, Sequence = number.Sequence };
    }
}
