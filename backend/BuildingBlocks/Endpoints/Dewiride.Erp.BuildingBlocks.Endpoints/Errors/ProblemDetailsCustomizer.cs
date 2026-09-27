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
            CamelCaseMemberPaths(validation.Errors);
        }
    }

    // Validation names a nested member by its CLR path (Customer.ShippingAddress.Street, OrderItems[0].Description), and the
    // serializer's DictionaryKeyPolicy converts only the start of a key, so each member name is converted here instead.
    private static void CamelCaseMemberPaths(IDictionary<string, string[]> errors)
    {
        var converted = new List<KeyValuePair<string, string[]>>(errors.Count);
        var changed = false;
        foreach (var (key, messages) in errors)
        {
            var path = ToMemberPath(key);
            changed |= !string.Equals(path, key, StringComparison.Ordinal);
            var index = converted.FindIndex(entry => string.Equals(entry.Key, path, StringComparison.Ordinal));
            if (index < 0)
            {
                converted.Add(new(path, messages));
            }
            else
            {
                converted[index] = new(path, [.. converted[index].Value, .. messages]);
            }
        }

        if (!changed)
        {
            return;
        }

        errors.Clear();
        foreach (var (path, messages) in converted)
        {
            errors.Add(path, messages);
        }
    }

    private static string ToMemberPath(string key)
    {
        var path = new StringBuilder(key.Length);
        var position = 0;
        while (position < key.Length)
        {
            if (key[position] == '.')
            {
                path.Append('.');
                position++;
            }
            else if (key[position] == '[')
            {
                var close = key.IndexOf(']', position);
                var end = close < 0 ? key.Length : close + 1;
                path.Append(key, position, end - position);
                position = end;
            }
            else
            {
                var length = key.AsSpan(position).IndexOfAny(MemberNameEnd);
                var name = key.Substring(position, length < 0 ? key.Length - position : length);
                path.Append(JsonNamingPolicy.CamelCase.ConvertName(name));
                position += name.Length;
            }
        }

        return path.ToString();
    }
}
