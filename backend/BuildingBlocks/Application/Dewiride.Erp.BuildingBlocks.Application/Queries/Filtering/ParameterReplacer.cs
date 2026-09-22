using System.Linq.Expressions;

namespace Dewiride.Erp.BuildingBlocks.Application.Queries.Filtering;

internal sealed class ParameterReplacer(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
{
    public static Expression Replace(Expression body, ParameterExpression from, ParameterExpression to) => new ParameterReplacer(from, to).Visit(body);

    protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : base.VisitParameter(node);
}
