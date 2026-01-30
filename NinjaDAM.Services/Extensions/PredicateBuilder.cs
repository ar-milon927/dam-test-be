using System;
using System.Linq.Expressions;

namespace NinjaDAM.Services.Extensions
{
    public static class PredicateBuilder
    {
        public static Expression<Func<T, bool>> True<T>() => param => true;
        public static Expression<Func<T, bool>> False<T>() => param => false;

        public static Expression<Func<T, bool>> And<T>(this Expression<Func<T, bool>> left, Expression<Func<T, bool>> right)
        {
            return Compose(left, right, Expression.AndAlso);
        }

        public static Expression<Func<T, bool>> Or<T>(this Expression<Func<T, bool>> left, Expression<Func<T, bool>> right)
        {
            return Compose(left, right, Expression.OrElse);
        }

        private static Expression<Func<T, bool>> Compose<T>(
            Expression<Func<T, bool>> left,
            Expression<Func<T, bool>> right,
            Func<Expression, Expression, Expression> merge)
        {
            var parameter = Expression.Parameter(typeof(T), "p");
            var leftBody = new ReplaceParameterVisitor(left.Parameters[0], parameter).Visit(left.Body);
            var rightBody = new ReplaceParameterVisitor(right.Parameters[0], parameter).Visit(right.Body);
            return Expression.Lambda<Func<T, bool>>(merge(leftBody!, rightBody!), parameter);
        }

        private sealed class ReplaceParameterVisitor : ExpressionVisitor
        {
            private readonly ParameterExpression _source;
            private readonly ParameterExpression _target;

            public ReplaceParameterVisitor(ParameterExpression source, ParameterExpression target)
            {
                _source = source;
                _target = target;
            }

            protected override Expression VisitParameter(ParameterExpression node)
            {
                if (node == _source)
                {
                    return _target;
                }

                return base.VisitParameter(node);
            }
        }
    }
}

