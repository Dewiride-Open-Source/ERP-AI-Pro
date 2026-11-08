namespace Dewiride.Erp.Modules.Platform.Numbering.Series.Endpoints.Responses;

/// <summary>A kind of document the ERP numbers, with its numbering.</summary>
/// <param name="DocumentType">Key of the kind of document.</param>
/// <param name="Title">What the documents are called, such as Tax invoices.</param>
/// <param name="Description">What the documents are, in one sentence.</param>
/// <param name="CurrentYear">Financial year of today in India, such as 2026-27.</param>
/// <param name="Rule">What the law allows the numbers in the current financial year; null when the ERP holds no rule for that year, so the documents cannot be numbered.</param>
/// <param name="Series">The numbering; null until it is set up.</param>
internal sealed record NumberedDocumentResponse(string DocumentType, string Title, string Description, string CurrentYear, SerialNumberRuleResponse? Rule, NumberSeriesResponse? Series);
