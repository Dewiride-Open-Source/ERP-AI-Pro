using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Dewiride.Erp.BuildingBlocks.Kernel.Results;

public sealed record Error(string Code, string Message, ErrorKind Kind)
{
    public IReadOnlyDictionary<string, string[]> Fields { get; init; } = ReadOnlyDictionary<string, string[]>.Empty;

    public static Error Validation(string code, string message) => new(code, message, ErrorKind.Validation);

    public static Error Validation(string code, string message, IReadOnlyDictionary<string, string[]> fields) =>
        new(code, message, ErrorKind.Validation) { Fields = fields };

    public static Error NotFound(string code, string message) => new(code, message, ErrorKind.NotFound);

    public static Error Conflict(string code, string message) => new(code, message, ErrorKind.Conflict);

    public static Error Forbidden(string code, string message) => new(code, message, ErrorKind.Forbidden);

    public static Error TooLarge(string code, string message) => new(code, message, ErrorKind.TooLarge);

    public static Error UnsupportedType(string code, string message) => new(code, message, ErrorKind.UnsupportedType);

    public static Error Failure(string code, string message) => new(code, message, ErrorKind.Failure);
}
