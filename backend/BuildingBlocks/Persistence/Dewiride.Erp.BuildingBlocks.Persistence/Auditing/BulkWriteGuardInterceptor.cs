using System.Linq.Expressions;
using Dewiride.Erp.BuildingBlocks.Kernel.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Dewiride.Erp.BuildingBlocks.Persistence.Auditing;

// A bulk statement goes straight to the store, past the stamps, the soft-delete conversion and the change records, so it runs
// only where none of them applies. Retention deletes append-only rows this way, which is why only their updates are refused.
public sealed class BulkWriteGuardInterceptor : IQueryExpressionInterceptor
{
    public Expression QueryCompilationStarting(Expression queryExpression, QueryExpressionEventData eventData)
    {
        if (queryExpression is MethodCallExpression { Method: { IsGenericMethod: true } method }
            && method.DeclaringType == typeof(EntityFrameworkQueryableExtensions))
        {
            var entityType = method.GetGenericArguments()[0];
            switch (method.Name)
            {
                case nameof(EntityFrameworkQueryableExtensions.ExecuteDelete) or nameof(EntityFrameworkQueryableExtensions.ExecuteDeleteAsync)
                    when typeof(ISoftDeletable).IsAssignableFrom(entityType):
                    throw new InvalidOperationException($"ExecuteDelete is not allowed on {entityType.Name} because it is soft-deletable; remove the entities through the change tracker so they are stamped instead of dropped.");
                case nameof(EntityFrameworkQueryableExtensions.ExecuteDelete) or nameof(EntityFrameworkQueryableExtensions.ExecuteDeleteAsync)
                    when typeof(IAuditable).IsAssignableFrom(entityType):
                    throw new InvalidOperationException($"ExecuteDelete is not allowed on {entityType.Name} because every deletion of it is recorded; remove the entities through the change tracker so each deletion leaves its change record.");
                case nameof(EntityFrameworkQueryableExtensions.ExecuteUpdate) or nameof(EntityFrameworkQueryableExtensions.ExecuteUpdateAsync)
                    when typeof(IAppendOnly).IsAssignableFrom(entityType):
                    throw new InvalidOperationException($"ExecuteUpdate is not allowed on {entityType.Name} because it is append-only; a saved row is never changed.");
                case nameof(EntityFrameworkQueryableExtensions.ExecuteUpdate) or nameof(EntityFrameworkQueryableExtensions.ExecuteUpdateAsync)
                    when typeof(IAuditable).IsAssignableFrom(entityType) || typeof(ISoftDeletable).IsAssignableFrom(entityType):
                    throw new InvalidOperationException($"ExecuteUpdate is not allowed on {entityType.Name} because its audit and deletion stamps are written by the change tracker; modify the tracked entities instead.");
            }
        }

        return queryExpression;
    }
}
