using System;

namespace Dewiride.Erp.BuildingBlocks.Kernel.Results;

public readonly record struct Result
{
    private Result(Error? error)
    {
        Error = error;
    }

    public Error? Error { get; }

    public bool IsSuccess => Error is null;

    public bool IsFailure => Error is not null;

    public static Result Success() => new(null);

    public static Result Fail(Error error) => new(error);

    public static Result<TValue> Success<TValue>(TValue value) => new(value, null);

    public static Result<TValue> Fail<TValue>(Error error) => new(default, error);

    public static implicit operator Result(Error error) => Fail(error);
}

public readonly record struct Result<TValue>
{
    private readonly TValue? _value;

    internal Result(TValue? value, Error? error)
    {
        _value = value;
        Error = error;
    }

    public Error? Error { get; }

    public bool IsSuccess => Error is null;

    public bool IsFailure => Error is not null;

    public TValue Value => IsSuccess ? _value! : throw new InvalidOperationException($"Result failed with {Error!.Code}; there is no value.");

    public TResult Match<TResult>(Func<TValue, TResult> onSuccess, Func<Error, TResult> onFailure) =>
        IsSuccess ? onSuccess(_value!) : onFailure(Error!);

    public static implicit operator Result<TValue>(TValue value) => new(value, null);

    public static implicit operator Result<TValue>(Error error) => new(default, error);
}
