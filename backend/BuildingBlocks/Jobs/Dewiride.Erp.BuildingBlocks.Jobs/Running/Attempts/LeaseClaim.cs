namespace Dewiride.Erp.BuildingBlocks.Jobs.Running.Attempts;

// The token fences the attempt: every later write of the attempt names it, so a runner whose lease another runner took over
// changes nothing.
internal sealed record LeaseClaim(Guid Token, string Runner, DateTimeOffset At, DateTimeOffset ExpiresAt, string TraceId);
