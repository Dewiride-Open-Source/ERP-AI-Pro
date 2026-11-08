namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.Companies.ScheduleII;

// Entry is Part C's reference as printed, such as IV(ii)(f)(5), with a worded suffix for the two crane lines printed under one
// number. Group holds the headings printed above the entry, joined by " > ", null for an item printed as one entry. Years is
// the useful life for single-shift working; ExtraShift is Nesd when the mark is printed on the entry or on a heading above it.
public sealed record UsefulLife(string Entry, string? Group, string Nature, int Years, ExtraShift ExtraShift);
