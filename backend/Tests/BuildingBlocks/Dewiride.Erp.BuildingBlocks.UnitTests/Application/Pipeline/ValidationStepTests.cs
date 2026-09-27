using System.ComponentModel.DataAnnotations;
using Dewiride.Erp.BuildingBlocks.Application.Pipeline;
using Dewiride.Erp.BuildingBlocks.Application.Pipeline.Steps;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Application.Pipeline;

public sealed class ValidationStepTests
{
    private const int RuleMaxLength = 20;

    private const string ObjectRule = "object";

    private const string BooleanObjectRule = "boolean-object";

    private const string ValidatableRule = "validatable";

    private const string ValidatableMemberRule = "validatable-member";

    private const string ObjectRuleMessage = "The probe is refused as a whole.";

    private const string BooleanObjectRuleMessage = "The probe fails a yes-or-no rule.";

    private const string ValidatableRuleMessage = "The probe refuses itself.";

    private const string ValidatableMemberRuleMessage = "The probe refuses its own rule.";

    private const string RuleTooLongMessage = "The rule is too long.";

    private static readonly HandlerDescriptor Descriptor = new(typeof(ValidationStepTests), typeof(ProbeCommand), HandlerKind.Command);

    [Theory]
    [InlineData(ObjectRule, "", ObjectRuleMessage)]
    [InlineData(BooleanObjectRule, "", BooleanObjectRuleMessage)]
    [InlineData(ValidatableRule, "", ValidatableRuleMessage)]
    [InlineData(ValidatableMemberRule, nameof(ProbeCommand.Rule), ValidatableMemberRuleMessage)]
    [InlineData("longer-than-twenty-characters", nameof(ProbeCommand.Rule), RuleTooLongMessage)]
    public async Task InvokeAsync_CommandBreakingOneRule_FailsWithItsMessageUnderTheKeyOfThatRule(string rule, string key, string message)
    {
        var result = await new ValidationStep().InvokeAsync(new ProbeCommand(rule, Nested: null), Descriptor, Unreachable, TestContext.Current.CancellationToken);

        Assert.Equal(ValidationStep.Code, result.Error!.Code);
        var field = Assert.Single(result.Error.Fields);
        Assert.Equal(key, field.Key);
        Assert.Equal([message], field.Value);
    }

    [Fact]
    public async Task InvokeAsync_CommandWhoseNestedObjectBreaksARule_ReachesTheHandler()
    {
        var result = await new ValidationStep().InvokeAsync(new ProbeCommand(Rule: null, new ProbeCommand(ObjectRule, Nested: null)), Descriptor, _ => Task.FromResult(Result.Success(1)), TestContext.Current.CancellationToken);

        Assert.Equal(1, result.Value);
    }

    private static Task<Result<int>> Unreachable(CancellationToken cancellationToken) =>
        throw new InvalidOperationException("The handler must not run for an invalid command.");

    [AttributeUsage(AttributeTargets.Class)]
    internal sealed class ProbeRuleAttribute : ValidationAttribute
    {
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext) =>
            value is ProbeCommand { Rule: ObjectRule } ? new ValidationResult(ObjectRuleMessage) : ValidationResult.Success;
    }

    [AttributeUsage(AttributeTargets.Class)]
    internal sealed class ProbeBooleanRuleAttribute : ValidationAttribute
    {
        public ProbeBooleanRuleAttribute()
            : base(BooleanObjectRuleMessage)
        {
        }

        public override bool IsValid(object? value) => value is not ProbeCommand { Rule: BooleanObjectRule };
    }

    [ProbeRule]
    [ProbeBooleanRule]
    internal sealed record ProbeCommand(
        [property: MaxLength(RuleMaxLength, ErrorMessage = RuleTooLongMessage)] string? Rule,
        ProbeCommand? Nested) : IValidatableObject
    {
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) => Rule switch
        {
            ValidatableRule => [new ValidationResult(ValidatableRuleMessage)],
            ValidatableMemberRule => [new ValidationResult(ValidatableMemberRuleMessage, [nameof(Rule)])],
            _ => [],
        };
    }
}
