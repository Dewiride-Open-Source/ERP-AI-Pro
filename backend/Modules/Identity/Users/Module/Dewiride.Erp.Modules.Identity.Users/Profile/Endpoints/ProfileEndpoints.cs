using Dewiride.Erp.BuildingBlocks.Application.Actors.PeopleDirectory;
using Dewiride.Erp.BuildingBlocks.Application.Commands;
using Dewiride.Erp.BuildingBlocks.Application.Queries;
using Dewiride.Erp.BuildingBlocks.Endpoints.Authorization;
using Dewiride.Erp.BuildingBlocks.Endpoints.Caching;
using Dewiride.Erp.BuildingBlocks.Endpoints.Results;
using Dewiride.Erp.Modules.Identity.Users.Profile.Application;
using Dewiride.Erp.Modules.Identity.Users.Profile.Application.Commands.ChooseOwnTheme;
using Dewiride.Erp.Modules.Identity.Users.Profile.Application.Queries.GetOwnProfile;
using Dewiride.Erp.Modules.Identity.Users.Profile.Endpoints.Requests;
using Dewiride.Erp.Modules.Identity.Users.Profile.Endpoints.Responses;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Dewiride.Erp.Modules.Identity.Users.Profile.Endpoints;

// Every route reads or changes the caller's own record only, so any signed-in person may call them, and none requires a
// permission (ModuleCatalogTests.SignedInRoutes). The photo is read from the company directory on the caller's behalf, so only
// a request can read it, as with the invitations. A person the directory holds no photo of is answered 204 rather than 404:
// the web app shows the photo as an image on every page, a browser reports every image answered 400 or above as a failed
// load, and an image without content leaves the initials in its place.
internal static class ProfileEndpoints
{
    private static readonly TimeSpan PhotoMaxAge = TimeSpan.FromHours(1);

    private static readonly string[] PhotoContentTypes = ["image/jpeg", "image/png", "image/gif", "image/webp"];

    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/me", GetAsync)
            .WithName("Identity.Users.GetOwnProfile")
            .WithSummary("Describes the signed-in person's own record: their details, today's position, the built-in roles they hold and the colour theme they chose.");

        group.MapGet("/me/photo", GetPhotoAsync)
            .WithName("Identity.Users.GetOwnPhoto")
            .WithSummary("Returns the signed-in person's photo from the company directory in Microsoft Entra, read on their behalf, or no content when the directory holds none.")
            .Produces<Stream>(StatusCodes.Status200OK, PhotoContentTypes[0], PhotoContentTypes[1..])
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .WithCallerDataCaching(PhotoMaxAge);

        group.MapPut("/me/theme", ChooseThemeAsync)
            .WithName("Identity.Users.ChooseOwnTheme")
            .WithSummary("Saves the colour theme the signed-in person chose, which the web app applies in every browser they sign in from.")
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static ProfileResponse ToResponse(OwnProfile profile) =>
        new(
            profile.Id,
            profile.DisplayName,
            profile.WorkEmail,
            profile.EmployeeCode,
            profile.PhoneNumber,
            profile.DateOfJoining,
            profile.DepartmentName,
            profile.DesignationName,
            profile.ManagerDisplayName,
            profile.Roles,
            profile.Theme);

    private static async Task<Results<Ok<ProfileResponse>, ProblemHttpResult>> GetAsync(
        IQueryHandler<GetOwnProfileQuery, OwnProfile> handler,
        ICallerRoles caller,
        CancellationToken cancellationToken)
    {
        var profile = await handler.HandleAsync(new GetOwnProfileQuery(caller.Roles), cancellationToken);

        return profile.IsSuccess ? TypedResults.Ok(ToResponse(profile.Value)) : profile.Error!.ToProblem();
    }

    private static async Task<Results<FileContentHttpResult, NoContent, ProblemHttpResult>> GetPhotoAsync(
        IPeopleDirectory directory,
        CancellationToken cancellationToken)
    {
        var photo = await directory.OwnPhotoAsync(cancellationToken);
        if (photo.IsSuccess)
        {
            return TypedResults.File(photo.Value.Content, photo.Value.ContentType);
        }

        return photo.Error == DirectoryErrors.PhotoNotFound ? TypedResults.NoContent() : photo.Error!.ToProblem();
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> ChooseThemeAsync(
        ChooseOwnThemeRequest request,
        ICommandHandler<ChooseOwnThemeCommand, Guid> handler,
        CancellationToken cancellationToken)
    {
        var chosen = await handler.HandleAsync(new ChooseOwnThemeCommand(request.Theme), cancellationToken);

        return chosen.IsSuccess ? TypedResults.NoContent() : chosen.Error!.ToProblem();
    }
}
