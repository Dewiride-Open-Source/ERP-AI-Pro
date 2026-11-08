using Dewiride.Erp.BuildingBlocks.Kernel.Time;
using Dewiride.Erp.BuildingBlocks.Numbering.Definitions;
using Dewiride.Erp.BuildingBlocks.Numbering.Rules;

namespace Dewiride.Erp.BuildingBlocks.Numbering.Administration;

// A kind of document with the rule its numbers follow in the current financial year, null when the ERP holds none, and its
// numbering, null until it is set up.
public sealed record NumberedDocumentDetails(NumberedDocument Document, FinancialYear CurrentYear, SerialNumberRule? Rule, NumberSeriesDetails? Series);
