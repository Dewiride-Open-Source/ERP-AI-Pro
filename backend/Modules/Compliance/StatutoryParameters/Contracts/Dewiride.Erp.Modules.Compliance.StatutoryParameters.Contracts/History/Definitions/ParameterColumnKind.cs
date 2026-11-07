namespace Dewiride.Erp.Modules.Compliance.StatutoryParameters.Contracts.History.Definitions;

public enum ParameterColumnKind
{
    Text = 1,
    Code = 2,
#pragma warning disable CA1720 // "integer" is the kind of value a column holds, as the seed files and the API name it, not a CLR type.
    Integer = 3,
#pragma warning restore CA1720
    Percent = 4,
    Amount = 5,
    Choice = 6,
    StateCode = 7,
}
