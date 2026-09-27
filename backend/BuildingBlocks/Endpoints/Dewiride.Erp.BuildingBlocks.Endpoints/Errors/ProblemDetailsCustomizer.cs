using System.Buffers;
using System.Text;
using System.Text.Json;
using Dewiride.Erp.BuildingBlocks.Endpoints.Correlation;
using Dewiride.Erp.BuildingBlocks.Endpoints.Results;
using Microsoft.AspNetCore.Http;

namespace Dewiride.Erp.BuildingBlocks.Endpoints.Errors;

internal static class ProblemDetailsCustomizer
{
    private static readonly SearchValues<char> MemberNameEnd = SearchValues.Create(".[");

    public static void Customize(ProblemDetailsContext context)
    {
        var problem = context.ProblemDetails;
        var status = problem.Status ?? context.HttpContext.Response.StatusCode;
        problem.Status = status;
        if (problem.Extensions.TryGetValue(ResultExtensions.CodeExtension, out var code) && code is string existing)
        {
            problem.Type = ProblemTypes.ToTypeUri(existing);
        }
        else
        {
            var defaultCode = ProblemTypes.DefaultCode(status);
            problem.Extensions[ResultExtensions.CodeExtension] = defaultCode;
            problem.Type = ProblemTypes.ToTypeUri(defaultCode);
        }

        problem.Instance ??= context.HttpContext.Request.Path;
        problem.Extensions[ProblemTypes.TraceIdExtension] = context.HttpContext.Features.Get<ICorrelationIdFeature>()?.CorrelationId ?? context.HttpContext.TraceIdentifier;

        if (problem is HttpValidationProblemDetails validation)
        {
            CamelCaseMemberPaths(validation);
        }
    }

    // Validation names a nested member by its CLR path (Customer.ShippingAddress.Street, OrderItems[0].Description), and the
    // serializer's DictionaryKeyPolicy converts only the start of a key, so each member name is converted here instead. A
    // request body yields a key per failing collection element, so keys merge through one ordinal hash lookup each, in the
    // order validation reported them.
    private static void CamelCaseMemberPaths(HttpValidationProblemDetails validation)
    {
        var converted = new OrderedDictionary<string, string[]>(validation.Errors.Count, StringComparer.Ordinal);
        var changed = false;
        foreach (var (key, messages) in validation.Errors)
        {
            var path = ToMemberPath(key);
            changed |= !string.Equals(path, key, StringComparison.Ordinal);
            if (!converted.TryAdd(path, messages, out var index))
            {
                converted.SetAt(index, [.. converted.GetAt(index).Value, .. messages]);
            }
        }

        if (changed)
        {
            validation.Errors = converted;
        }
    }

    private static string ToMemberPath(string key)
    {
        StringBuilder? path = null;
        var position = 0;
        while (position < key.Length)
        {
            var end = SegmentEnd(key, position);
            if (char.IsUpper(key[position]))
            {
                path ??= new StringBuilder(key.Length).Append(key, 0, position);
                path.Append(JsonNamingPolicy.CamelCase.ConvertName(key[position..end]));
            }
            else
            {
                path?.Append(key, position, end - position);
            }

            position = end;
        }

        return path?.ToString() ?? key;
    }

    private static int SegmentEnd(string key, int start)
    {
        if (key[start] == '.')
        {
            return start + 1;
        }

        if (key[start] == '[')
        {
            var close = key.IndexOf(']', start);
            return close < 0 ? key.Length : close + 1;
        }

        var length = key.AsSpan(start).IndexOfAny(MemberNameEnd);
        return length < 0 ? key.Length : start + length;
    }
}
