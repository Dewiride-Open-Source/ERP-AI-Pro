namespace Dewiride.Erp.BuildingBlocks.Authentication.Sessions;

// Read by SessionCookieEvents.CheckSlidingExpiration on the session endpoints only: a page reads how long its session has
// left without extending it, so a session still ends after the idle timeout without a request from the person, and renews
// it at once when the person chooses to stay signed in, however recently it was last renewed.
internal sealed record SessionRenewalMetadata(bool Renews);
