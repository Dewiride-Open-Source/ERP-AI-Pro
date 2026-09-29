using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using Dewiride.Erp.BuildingBlocks.Endpoints.Correlation;
using Dewiride.Erp.BuildingBlocks.Endpoints.Errors;
using Dewiride.Erp.BuildingBlocks.Endpoints.Results;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;
using Dewiride.Erp.Testing;
using Dewiride.Erp.Testing.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Dewiride.Erp.Host.Api.IntegrationTests.Pipeline;

public sealed class ProblemDetailsTests : IClassFixture<ProblemDetailsTests.Fixture>
{
    private const string ProblemEndpoint = "/__test/conflict";

    private const string ValidationEndpoint = "/__test/invalid-order";

    private const string NodeEndpoint = "/__test/nodes";

    private const int RuleMaxLength = 20;

    private const string ObjectRule = "object";

    private const string ObjectMemberRule = "object-member";

    private const string BooleanObjectRule = "boolean-object";

    private const string ValidatableRule = "validatable";

    private const string ValidatableMemberRule = "validatable-member";

    private const string ObjectRuleMessage = "The node is refused as a whole.";

    private const string ObjectMemberRuleMessage = "The node refuses its rule.";

    private const string BooleanObjectRuleMessage = "The node fails a yes-or-no rule.";

    private const string ValidatableRuleMessage = "The node refuses itself.";

    private const string ValidatableMemberRuleMessage = "The node refuses its own rule.";

    private const string RuleTooLongMessage = "The rule is too long.";

    private readonly HttpClient _client;

    public ProblemDetailsTests(Fixture fixture)
    {
        _client = fixture.Factory.CreateClient().AsUser(TestUsers.Accountant);
    }

    [Fact]
    public async Task Get_UnknownRoute_AnswersTheNotFoundProblemType()
    {
        using var response = await _client.GetAsync(new Uri("/api/platform/does-not-exist", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await ReadAsync(response);
        Assert.Equal(StatusCodes.Status404NotFound, problem.Status);
        Assert.Equal("/problems/resource.not-found", problem.Type);
        Assert.Equal(ProblemTypes.ResourceNotFound, problem.Extensions[ResultExtensions.CodeExtension]?.ToString());
        Assert.Equal("/api/platform/does-not-exist", problem.Instance);
        Assert.Equal(Assert.Single(response.Headers.GetValues(CorrelationId.HeaderName)), problem.Extensions["traceId"]?.ToString());
    }

    [Fact]
    public async Task Post_RouteThatOnlyAllowsGet_AnswersAClientErrorProblemType()
    {
        using var response = await _client.PostAsync(new Uri("/api/platform/system-info", UriKind.Relative), content: null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await ReadAsync(response);
        Assert.Equal("/problems/request.method-not-allowed", problem.Type);
        Assert.Equal(ProblemTypes.RequestMethodNotAllowed, problem.Extensions[ResultExtensions.CodeExtension]?.ToString());
        Assert.Equal(Assert.Single(response.Headers.GetValues(CorrelationId.HeaderName)), problem.Extensions["traceId"]?.ToString());
    }

    [Fact]
    public async Task Get_ThrowingRoute_AnswersTheServerErrorProblemType()
    {
        using var response = await _client.GetAsync(new Uri(ErpApiFactory.ThrowingPath, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var problem = await ReadAsync(response);
        Assert.Equal("/problems/server.error", problem.Type);
        Assert.Equal(ProblemTypes.ServerError, problem.Extensions[ResultExtensions.CodeExtension]?.ToString());
        Assert.Equal(ErpApiFactory.ThrowingPath, problem.Instance);
        Assert.Equal(Assert.Single(response.Headers.GetValues(CorrelationId.HeaderName)), problem.Extensions["traceId"]?.ToString());
    }

    [Fact]
    public async Task Get_EndpointReturningAnError_AnswersTheProblemTypeOfThatErrorCode()
    {
        using var response = await _client.GetAsync(new Uri(ProblemEndpoint, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await ReadAsync(response);
        Assert.Equal("/problems/invoice.already-issued", problem.Type);
        Assert.Equal("invoice.already-issued", problem.Extensions[ResultExtensions.CodeExtension]?.ToString());
        Assert.Equal("The invoice was already issued.", problem.Detail);
        Assert.Equal(ProblemEndpoint, problem.Instance);
        Assert.Equal(Assert.Single(response.Headers.GetValues(CorrelationId.HeaderName)), problem.Extensions["traceId"]?.ToString());
    }

    [Fact]
    public async Task Get_EndpointReturningAValidationErrorOnNestedMembers_AnswersCamelCaseMemberPaths()
    {
        using var response = await _client.GetAsync(new Uri(ValidationEndpoint, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var errors = await ReadErrorsAsync(response);
        Assert.Equal(
            ["", "customer.shippingAddress.street", "gstin", "orderItems[0].description"],
            errors.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(["Enter the street."], errors["customer.shippingAddress.street"]);
        Assert.Equal(["Enter a description."], errors["orderItems[0].description"]);
        Assert.Equal(["Enter a GSTIN."], errors["gstin"]);
        Assert.Equal(["The order is empty."], errors[""]);
    }

    [Theory]
    [InlineData("""{"rule":"object"}""", "", ObjectRuleMessage)]
    [InlineData("""{"nested":{"rule":"object"}}""", "nested", ObjectRuleMessage)]
    [InlineData("""{"nestedItems":[{},{"rule":"object"}]}""", "nestedItems[1]", ObjectRuleMessage)]
    [InlineData("""{"nested":{"nestedItems":[{"rule":"object"}]}}""", "nested.nestedItems[0]", ObjectRuleMessage)]
    [InlineData("""{"nested":{"rule":"object-member"}}""", "nested.rule", ObjectMemberRuleMessage)]
    [InlineData("""{"nested":{"rule":"boolean-object"}}""", "nested.nestedItems", BooleanObjectRuleMessage)]
    [InlineData("""{"rule":"validatable"}""", "", ValidatableRuleMessage)]
    [InlineData("""{"nested":{"rule":"validatable"}}""", "", ValidatableRuleMessage)]
    [InlineData("""{"nestedItems":[{},{"rule":"validatable"}]}""", "", ValidatableRuleMessage)]
    [InlineData("""{"nested":{"rule":"validatable-member"}}""", "nested.rule", ValidatableMemberRuleMessage)]
    [InlineData("""{"nestedItems":[{"rule":"longer-than-twenty-characters"}]}""", "nestedItems[0].rule", RuleTooLongMessage)]
    [InlineData("""{"rule":"object","nested":{"rule":"validatable"}}""", "", ValidatableRuleMessage)]
    public async Task Post_BodyBreakingOneValidationRule_AnswersItsMessageUnderTheKeyOfThatRule(string body, string key, string message)
    {
        using var content = new StringContent(body, Encoding.UTF8, MediaTypeNames.Application.Json);
        using var response = await _client.PostAsync(new Uri(NodeEndpoint, UriKind.Relative), content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = Assert.Single(await ReadErrorsAsync(response));
        Assert.Equal(key, error.Key);
        Assert.Equal([message], error.Value);
    }

    private static async Task<ProblemDetails> ReadAsync(HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.NotNull(problem);

        return problem;
    }

    private static async Task<Dictionary<string, string?[]>> ReadErrorsAsync(HttpResponseMessage response)
    {
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken), cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("/problems/request.invalid", body.RootElement.GetProperty("type").GetString());

        return body.RootElement.GetProperty("errors").EnumerateObject().ToDictionary(
            member => member.Name,
            member => member.Value.EnumerateArray().Select(message => message.GetString()).ToArray(),
            StringComparer.Ordinal);
    }

    public sealed class Fixture : IDisposable
    {
        private readonly ErpApiFactory _factory;

        public Fixture()
        {
            _factory = new ErpApiFactory().WithTestEndpoints(routes =>
            {
                routes.MapGet(ProblemEndpoint, () => Error.Conflict("invoice.already-issued", "The invoice was already issued.").ToProblem());
                routes.MapGet(ValidationEndpoint, () => Error.Validation(ProblemTypes.RequestInvalid, "OrderRequest is invalid.", new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["Customer.ShippingAddress.Street"] = ["Enter the street."],
                    ["OrderItems[0].Description"] = ["Enter a description."],
                    ["GSTIN"] = ["Enter a GSTIN."],
                    [""] = ["The order is empty."],
                }).ToProblem());
                routes.MapPost(NodeEndpoint, (ValidatedNode node) => Results.NoContent());
            });
            Factory = _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddValidation()));
        }

        public WebApplicationFactory<Program> Factory { get; }

        public void Dispose() => _factory.Dispose();
    }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class NodeRuleAttribute : ValidationAttribute
    {
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext) => (value as ValidatedNode)?.Rule switch
        {
            ObjectRule => new ValidationResult(ObjectRuleMessage),
            ObjectMemberRule => new ValidationResult(ObjectMemberRuleMessage, [nameof(ValidatedNode.Rule)]),
            _ => ValidationResult.Success,
        };
    }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class NodeBooleanRuleAttribute : ValidationAttribute
    {
        public NodeBooleanRuleAttribute()
            : base(BooleanObjectRuleMessage)
        {
        }

        public override bool IsValid(object? value) => value is not ValidatedNode { Rule: BooleanObjectRule };
    }

    [NodeRule]
    [NodeBooleanRule]
    public sealed record ValidatedNode(
        [property: MaxLength(RuleMaxLength, ErrorMessage = RuleTooLongMessage)] string? Rule,
        ValidatedNode? Nested,
        IReadOnlyList<ValidatedNode>? NestedItems) : IValidatableObject
    {
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) => Rule switch
        {
            ValidatableRule => [new ValidationResult(ValidatableRuleMessage)],
            ValidatableMemberRule => [new ValidationResult(ValidatableMemberRuleMessage, [nameof(Rule)])],
            _ => [],
        };
    }
}
