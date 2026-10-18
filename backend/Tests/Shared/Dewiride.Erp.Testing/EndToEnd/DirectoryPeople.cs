using Dewiride.Erp.BuildingBlocks.Application.Actors.PeopleDirectory;
using Dewiride.Erp.Testing.Graph;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Dewiride.Erp.Testing.EndToEnd;

// The end-to-end host answers Microsoft Graph from its test directory, which holds only the people the specs add here, so a
// spec adds every person it searches for, invites or shows the photo of. Like the persona sign-in, the route is the test
// host's own: anonymous, taking no antiforgery token whether or not the request carries a session, and mapped only in the
// Testing environment. Adding a person again replaces them, so a spec disables or deletes a person's account for the
// directory's account check by adding them again with the account state "disabled" or "deleted"; absent means enabled.
public static class DirectoryPeople
{
    public const string Path = "/api/__test/directory/people";

    private static readonly string[] PhotoContentTypes = ["image/jpeg", "image/png", "image/gif", "image/webp"];

    public static void Map(IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        var environment = routes.ServiceProvider.GetRequiredService<IHostEnvironment>();
        if (!environment.IsEnvironment(EndToEndHost.EnvironmentName))
        {
            throw new InvalidOperationException(
                $"The test directory's people are added by anyone, so the route is mapped only in the {EndToEndHost.EnvironmentName} environment; this host runs in {environment.EnvironmentName}.");
        }

        routes.MapPost(Path, Add).AllowAnonymous().DisableAntiforgery();
    }

    private static Results<NoContent, ValidationProblem> Add(DirectoryPersonSeed seed, TestDirectory directory)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var objectId = seed.ObjectId ?? Guid.Empty;
        if (objectId == Guid.Empty)
        {
            errors["objectId"] = ["Name the Entra object id of the person."];
        }

        if (string.IsNullOrWhiteSpace(seed.DisplayName))
        {
            errors["displayName"] = ["Name the person."];
        }

        if (string.IsNullOrWhiteSpace(seed.UserPrincipalName))
        {
            errors["userPrincipalName"] = ["Give the user principal name of the person."];
        }

        var photo = seed.Photo is null ? null : PhotoOf(seed.Photo);
        if (seed.Photo is not null && photo is null)
        {
            errors["photo"] = ["A photo is a JPEG, PNG, GIF or WebP image in base64."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        directory.Add(new TestDirectoryPerson(
            objectId,
            seed.DisplayName!.Trim(),
            seed.UserPrincipalName!.Trim(),
            string.IsNullOrWhiteSpace(seed.Mail) ? null : seed.Mail.Trim(),
            photo,
            seed.AccountState ?? DirectoryAccountState.Enabled));

        return TypedResults.NoContent();
    }

    private static TestDirectoryPhoto? PhotoOf(DirectoryPhotoSeed seed)
    {
        if (seed.ContentType is not { } contentType || !PhotoContentTypes.Contains(contentType, StringComparer.Ordinal) || string.IsNullOrEmpty(seed.Base64))
        {
            return null;
        }

        try
        {
            return new TestDirectoryPhoto(Convert.FromBase64String(seed.Base64), contentType);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
