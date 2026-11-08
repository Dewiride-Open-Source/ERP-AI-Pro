using Dewiride.Erp.BuildingBlocks.Endpoints.Results;
using Dewiride.Erp.Testing.Numbering;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Dewiride.Erp.Testing.EndToEnd.Numbering;

// No module issues documents yet, so a spec shows a financial year that has given numbers by having the issues route take them,
// dated today in India, exactly as an issuing command would, and starts from a kind without numbering through the resets route.
// Both accept only the test kinds of TestNumberedDocuments. Like the other test routes, they are the test host's own: anonymous,
// taking no antiforgery token whether or not the request carries a session, and mapped only in the Testing environment.
public static class NumberingRoutes
{
    public const string IssuesPath = "/api/__test/numbering/issues";

    public const string ResetsPath = "/api/__test/numbering/resets";

    public const int MaxCount = 100;

    public static void Map(IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        var environment = routes.ServiceProvider.GetRequiredService<IHostEnvironment>();
        if (!environment.IsEnvironment(EndToEndHost.EnvironmentName))
        {
            throw new InvalidOperationException(
                $"Anyone can take or remove document numbers here, so the routes are mapped only in the {EndToEndHost.EnvironmentName} environment; this host runs in {environment.EnvironmentName}.");
        }

        routes.MapPost(IssuesPath, IssueAsync).AllowAnonymous().DisableAntiforgery();
        routes.MapPost(ResetsPath, ResetAsync).AllowAnonymous().DisableAntiforgery();
    }

    private static async Task<Results<Ok<NumberIssueResponse>, ValidationProblem, ProblemHttpResult>> IssueAsync(NumberIssue issue, HttpContext context)
    {
        if (!IsTestKind(issue.DocumentType) || issue.Count is < 1 or > MaxCount)
        {
            return Refusal($"Name a test kind of document and take 1 to {MaxCount} numbers.");
        }

        var issued = await NumberIssuer.IssueAsync(context.RequestServices, issue.DocumentType, issue.Count, context.RequestAborted);

        return issued.IsSuccess ? TypedResults.Ok(new NumberIssueResponse([.. issued.Value.Select(number => number.Text)])) : issued.Error!.ToProblem();
    }

    private static async Task<Results<NoContent, ValidationProblem>> ResetAsync(NumberingReset reset, HttpContext context)
    {
        if (!IsTestKind(reset.DocumentType))
        {
            return Refusal("Name a test kind of document.");
        }

        await NumberingResets.ResetAsync(context.RequestServices, reset.DocumentType, context.RequestAborted);

        return TypedResults.NoContent();
    }

    private static bool IsTestKind(string? documentType) =>
        TestNumberedDocuments.All.Any(document => string.Equals(document.Key, documentType, StringComparison.Ordinal));

    private static ValidationProblem Refusal(string message) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]>(StringComparer.Ordinal) { ["documentType"] = [message] });
}
