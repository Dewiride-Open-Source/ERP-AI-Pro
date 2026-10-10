namespace Dewiride.Erp.BuildingBlocks.Auditing.Security;

public enum SecurityEventKind : byte
{
    SignedIn = 1,
    SignInFailed = 2,
    SignedOut = 3,
    FrontChannelSignedOut = 4,
    FrontChannelSignOutRefused = 5,
    BearerTokenRefused = 6,
    SignInRefused = 7,
}
