using System.Linq.Expressions;
using Dewiride.Erp.BuildingBlocks.Kernel.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Dewiride.Erp.BuildingBlocks.Persistence.Auditing;

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
                case nameof(EntityFrameworkQueryableExtensions.ExecuteUpdate) or nameof(EntityFrameworkQueryableExtensions.ExecuteUpdateAsync)
                    when typeof(IAuditable).IsAssignableFrom(entityType) || typeof(ISoftDeletable).IsAssignableFrom(entityType):
                    throw new InvalidOperationException($"ExecuteUpdate is not allowed on {entityType.Name} because its audit and deletion stamps are written by the change tracker; modify the tracked entities instead.");
            }
        }

        return queryExpression;
    }
}
