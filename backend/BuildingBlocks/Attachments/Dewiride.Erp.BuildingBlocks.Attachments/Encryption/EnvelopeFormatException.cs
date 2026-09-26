namespace Dewiride.Erp.BuildingBlocks.Attachments.Encryption;

internal sealed class EnvelopeFormatException : Exception
{
    public EnvelopeFormatException()
    {
    }

    public EnvelopeFormatException(string message)
        : base(message)
    {
    }

    public EnvelopeFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
