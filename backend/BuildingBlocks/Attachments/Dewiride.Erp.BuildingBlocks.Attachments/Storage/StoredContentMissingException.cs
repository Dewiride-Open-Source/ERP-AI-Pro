namespace Dewiride.Erp.BuildingBlocks.Attachments.Storage;

internal sealed class StoredContentMissingException : Exception
{
    public StoredContentMissingException()
    {
    }

    public StoredContentMissingException(string message)
        : base(message)
    {
    }

    public StoredContentMissingException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
