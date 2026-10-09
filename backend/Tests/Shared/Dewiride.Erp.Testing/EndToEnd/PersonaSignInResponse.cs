namespace Dewiride.Erp.Testing.EndToEnd;

public sealed record PersonaSignInResponse(Guid Id, string Name, string UserName, IReadOnlyList<string> Roles, string EntraSessionId, string Issuer);
