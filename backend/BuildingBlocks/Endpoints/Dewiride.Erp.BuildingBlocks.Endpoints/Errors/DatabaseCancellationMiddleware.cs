using System.Data.Common;
using Microsoft.AspNetCore.Http;

namespace Dewiride.Erp.BuildingBlocks.Endpoints.Errors;

// A database driver reports a command cancelled through RequestAborted as its own exception (SqlClient raises SqlException),
// while the request-timeout and exception-handler middleware recognise only OperationCanceledException; translating it here
// lets a timed-out query answer 504 and an abandoned one 499 instead of a logged 500.
internal sealed class DatabaseCancellationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context).ConfigureAwait(false);
        }
        catch (Exception exception) when (context.RequestAborted.IsCancellationRequested && IsDatabaseFailure(exception))
        {
            throw new OperationCanceledException("The database command was cancelled with the request.", exception, context.RequestAborted);
        }
    }

    private static bool IsDatabaseFailure(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is DbException)
            {
                return true;
            }
        }

        return false;
    }
}
