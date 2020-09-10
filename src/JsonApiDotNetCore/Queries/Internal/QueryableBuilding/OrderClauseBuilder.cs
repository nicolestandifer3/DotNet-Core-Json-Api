using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using JsonApiDotNetCore.Queries.Expressions;
using JsonApiDotNetCore.Resources.Annotations;

namespace JsonApiDotNetCore.Queries.Internal.QueryableBuilding
{
    /// <summary>
    /// Transforms <see cref="SortExpression"/> into <see cref="Queryable.OrderBy{TSource, TKey}(IQueryable{TSource}, Expression{Func{TSource,TKey}})"/> calls.
    /// </summary>
    public class OrderClauseBuilder : QueryClauseBuilder<Expression>
    {
        private readonly Expression _source;
        private readonly Type _extensionType;

        public OrderClauseBuilder(Expression source, LambdaScope lambdaScope, Type extensionType)
            : base(lambdaScope)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _extensionType = extensionType ?? throw new ArgumentNullException(nameof(extensionType));
        }

        public Expression ApplyOrderBy(SortExpression expression)
        {
            if (expression == null)
            {
                throw new ArgumentNullException(nameof(expression));
            }

            return Visit(expression, null);
        }

        public override Expression VisitSort(SortExpression expression, Expression argument)
        {
            Expression sortExpression = null;

            foreach (SortElementExpression sortElement in expression.Elements)
            {
                sortExpression = Visit(sortElement, sortExpression);
            }

            return sortExpression;
        }

        public override Expression VisitSortElement(SortElementExpression expression, Expression previousExpression)
        {
            Expression body = expression.Count != null
                ? Visit(expression.Count, null)
                : Visit(expression.TargetAttribute, null);

            LambdaExpression lambda = Expression.Lambda(body, LambdaScope.Parameter);

            string operationName = previousExpression == null ? 
                expression.IsAscending ? "OrderBy" : "OrderByDescending" :
                expression.IsAscending ? "ThenBy" : "ThenByDescending";

            return ExtensionMethodCall(previousExpression ?? _source, operationName, body.Type, lambda);
        }

        private Expression ExtensionMethodCall(Expression source, string operationName, Type keyType,
            LambdaExpression keySelector)
        {
            return Expression.Call(_extensionType, operationName, new[]
            {
                LambdaScope.Parameter.Type,
                keyType
            }, source, keySelector);
        }

        protected override MemberExpression CreatePropertyExpressionForFieldChain(IReadOnlyCollection<ResourceFieldAttribute> chain, Expression source)
        {
            var components = chain.Select(field =>
                // In case of a HasManyThrough access (from count() function), we only need to look at the number of entries in the join table.
                field is HasManyThroughAttribute hasManyThrough ? hasManyThrough.ThroughProperty.Name : field.Property.Name).ToArray();

            return CreatePropertyExpressionFromComponents(LambdaScope.Accessor, components);
        }
    }
}
