using System;
using System.Linq;
using System.Linq.Expressions;
using JsonApiDotNetCore.Queries.Expressions;

namespace JsonApiDotNetCore.Queries.Internal.QueryableBuilding
{
    /// <summary>
    /// Transforms <see cref="PaginationExpression"/> into <see cref="Queryable.Skip{TSource}"/> and <see cref="Queryable.Take{TSource}"/> calls.
    /// </summary>
    public class SkipTakeClauseBuilder : QueryClauseBuilder<object>
    {
        private readonly Expression _source;
        private readonly Type _extensionType;

        public SkipTakeClauseBuilder(Expression source, LambdaScope lambdaScope, Type extensionType)
            : base(lambdaScope)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _extensionType = extensionType ?? throw new ArgumentNullException(nameof(extensionType));
        }

        public Expression ApplySkipTake(PaginationExpression expression)
        {
            if (expression == null)
            {
                throw new ArgumentNullException(nameof(expression));
            }

            return Visit(expression, null);
        }

        public override Expression VisitPagination(PaginationExpression expression, object argument)
        {
            Expression skipTakeExpression = _source;

            if (expression.PageSize != null)
            {
                int skipValue = (expression.PageNumber.OneBasedValue - 1) * expression.PageSize.Value;

                if (skipValue > 0)
                {
                    skipTakeExpression = ExtensionMethodCall(skipTakeExpression, "Skip", skipValue);
                }

                skipTakeExpression = ExtensionMethodCall(skipTakeExpression, "Take", expression.PageSize.Value);
            }

            return skipTakeExpression;
        }

        private Expression ExtensionMethodCall(Expression source, string operationName, int value)
        {
            Expression constant = CreateTupleAccessExpressionForConstant(value, typeof(int));

            return Expression.Call(_extensionType, operationName, new[]
            {
                LambdaScope.Parameter.Type
            }, source, constant);
        }
    }
}
