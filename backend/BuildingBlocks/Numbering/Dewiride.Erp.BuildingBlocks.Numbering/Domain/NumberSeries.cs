using Dewiride.Erp.BuildingBlocks.Kernel.Domain;
using Dewiride.Erp.BuildingBlocks.Numbering.Definitions;
using Dewiride.Erp.BuildingBlocks.Numbering.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dewiride.Erp.BuildingBlocks.Numbering.Domain;

// The numbering of one kind of document: the format a financial year takes when its first number is given or its next number is
// set. A year keeps its own copy, so once it has given a number a change applies from the next year.
[EntityTypeConfiguration(typeof(NumberSeriesConfiguration))]
public sealed class NumberSeries : AggregateRoot<NumberSeriesId>, IAuditable, IVersioned
{
    public const int DocumentTypeMaxLength = NumberedDocument.KeyMaxLength;

    private NumberSeries(NumberSeriesId id)
        : base(id)
    {
    }

    private NumberSeries(NumberSeriesId id, string documentType, NumberFormat format)
        : base(id)
    {
        DocumentType = documentType;
        Format = format;
    }

    public string DocumentType { get; private set; } = string.Empty;

    public NumberFormat Format { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public Guid? ModifiedBy { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    internal static NumberSeries Create(string documentType, NumberFormat format)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentType);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(documentType.Length, DocumentTypeMaxLength, nameof(documentType));

        return new NumberSeries(NumberSeriesId.Create(), documentType, format);
    }

    internal void Change(NumberFormat format) => Format = format;
}
