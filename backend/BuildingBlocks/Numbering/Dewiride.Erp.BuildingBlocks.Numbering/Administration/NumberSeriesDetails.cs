using Dewiride.Erp.BuildingBlocks.Numbering.Domain;

namespace Dewiride.Erp.BuildingBlocks.Numbering.Administration;

// The format the next year to open takes, and the years opened so far, the latest first.
public sealed record NumberSeriesDetails(NumberSeriesId Id, NumberFormat Format, string Version, IReadOnlyList<NumberSeriesYearDetails> Years);
