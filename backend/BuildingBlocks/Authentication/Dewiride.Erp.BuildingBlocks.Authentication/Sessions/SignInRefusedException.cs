namespace Dewiride.Erp.BuildingBlocks.Authentication.Sessions;

internal sealed class SignInRefusedException : Exception
{
    public const string DefaultMessage = "The person's record does not let them sign in.";

    public SignInRefusedException()
        : base(DefaultMessage)
    {
    }

    public SignInRefusedException(Exception innerException)
        : base(DefaultMessage, innerException)
    {
    }

    public SignInRefusedException(string message)
        : base(message)
    {
    }

    public SignInRefusedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
