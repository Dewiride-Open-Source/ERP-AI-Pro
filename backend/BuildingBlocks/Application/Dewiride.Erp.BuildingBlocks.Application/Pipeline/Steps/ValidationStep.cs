using System.ComponentModel.DataAnnotations;
using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.BuildingBlocks.Application.Pipeline.Steps;

internal sealed class ValidationStep : IPipelineStep
{
    public const string Code = "request.invalid";

    public PipelineStage Stage => PipelineStage.Validation;

    public Task<Result<TResult>> InvokeAsync<TRequest, TResult>(TRequest request, HandlerDescriptor descriptor, PipelineContinuation<TResult> next, CancellationToken cancellationToken)
        where TRequest : notnull
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(next);

        var failures = new List<ValidationResult>();
        if (Validator.TryValidateObject(request, new ValidationContext(request), failures, validateAllProperties: true))
        {
            return next(cancellationToken);
        }

        var fields = failures
            .SelectMany(failure => (failure.MemberNames.Any() ? failure.MemberNames : [string.Empty]).Select(member => (Member: member, Message: failure.ErrorMessage ?? "The value is invalid.")))
            .GroupBy(failure => failure.Member, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(failure => failure.Message).ToArray(), StringComparer.Ordinal);

        return Task.FromResult<Result<TResult>>(Error.Validation(Code, $"{descriptor.RequestType.Name} is invalid.", fields));
    }
}
