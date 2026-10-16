using Dewiride.Erp.BuildingBlocks.Persistence.Auditing.Changes;

namespace Dewiride.Erp.Modules.Platform.Audit.Changes.Endpoints.Responses;

/// <summary>One field a change touched, with its values before and after the change.</summary>
/// <param name="Property">Path of the field in camelCase, with a member of a value object after a dot, such as price.amount.</param>
/// <param name="Kind">What the field holds, which decides how its values read.</param>
/// <param name="Before">Value before the change as text, a list as compact JSON; null when the field was empty or the change created the record.</param>
/// <param name="After">Value after the change as text, a list as compact JSON; null when the field became empty or the change removed the record.</param>
/// <param name="Reference">Change key of the type of record the field refers to, when the field is a reference, whose values are then that record's ids.</param>
/// <param name="BeforeName">Name the record the value before refers to has today; null when the field is no reference or no name is found.</param>
/// <param name="AfterName">Name the record the value after refers to has today; null when the field is no reference or no name is found.</param>
internal sealed record ChangedValueResponse(
    string Property,
    ChangeValueKind Kind,
    string? Before,
    string? After,
    string? Reference,
    string? BeforeName,
    string? AfterName);
