using Dewiride.Erp.BuildingBlocks.Kernel.Results;

namespace Dewiride.Erp.BuildingBlocks.UnitTests.Kernel.Results;

public sealed class ResultTests
{
    private static readonly Error SampleError = Error.NotFound("sample.missing", "The sample was not found.");

    [Fact]
    public void Success_HasNoErrorAndExposesValue()
    {
        var result = Result.Success(42);

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Null(result.Error);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Fail_ExposesErrorAndValueThrows()
    {
        var result = Result.Fail<int>(SampleError);

        Assert.True(result.IsFailure);
        Assert.Same(SampleError, result.Error);
        var exception = Assert.Throws<InvalidOperationException>(() => result.Value);
        Assert.Contains(SampleError.Code, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Match_SelectsTheBranchForTheOutcome()
    {
        Assert.Equal("value 7", Result.Success(7).Match(v => $"value {v}", e => e.Code));
        Assert.Equal(SampleError.Code, Result.Fail<int>(SampleError).Match(v => $"value {v}", e => e.Code));
    }

    [Fact]
    public void ImplicitConversions_WrapValuesAndErrors()
    {
        Result<string> fromValue = "hello";
        Result<string> fromError = SampleError;
        Result nonGenericFromError = SampleError;

        Assert.Equal("hello", fromValue.Value);
        Assert.Same(SampleError, fromError.Error);
        Assert.Same(SampleError, nonGenericFromError.Error);
        Assert.True(Result.Success().IsSuccess);
    }

    [Theory]
    [InlineData(ErrorKind.Validation)]
    [InlineData(ErrorKind.NotFound)]
    [InlineData(ErrorKind.Conflict)]
    [InlineData(ErrorKind.Forbidden)]
    [InlineData(ErrorKind.Failure)]
    public void ErrorFactories_StampTheKind(ErrorKind kind)
    {
        var error = kind switch
        {
            ErrorKind.Validation => Error.Validation("c", "m"),
            ErrorKind.NotFound => Error.NotFound("c", "m"),
            ErrorKind.Conflict => Error.Conflict("c", "m"),
            ErrorKind.Forbidden => Error.Forbidden("c", "m"),
            _ => Error.Failure("c", "m"),
        };

        Assert.Equal(kind, error.Kind);
        Assert.Equal("c", error.Code);
        Assert.Equal("m", error.Message);
    }
}
