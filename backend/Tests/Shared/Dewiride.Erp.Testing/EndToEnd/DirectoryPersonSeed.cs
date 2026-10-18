namespace Dewiride.Erp.Testing.EndToEnd;

public sealed record DirectoryPersonSeed(Guid? ObjectId, string? DisplayName, string? UserPrincipalName, string? Mail, DirectoryPhotoSeed? Photo);
