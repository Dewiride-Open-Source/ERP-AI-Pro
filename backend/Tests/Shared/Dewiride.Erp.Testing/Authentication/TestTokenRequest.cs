namespace Dewiride.Erp.Testing.Authentication;

public sealed record TestTokenRequest(Uri Address, IReadOnlyDictionary<string, string> Form);
