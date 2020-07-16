using System;
using Humanizer;

namespace JsonApiDotNetCore.Internal.Queries.Expressions
{
    public class ComparisonExpression : FilterExpression
    {
        public ComparisonOperator Operator { get; }
        public QueryExpression Left { get; }
        public QueryExpression Right { get; }

        public ComparisonExpression(ComparisonOperator @operator, QueryExpression left, QueryExpression right)
        {
            Operator = @operator;
            Left = left ?? throw new ArgumentNullException(nameof(left));
            Right = right ?? throw new ArgumentNullException(nameof(right));
        }

        public override TResult Accept<TArgument, TResult>(QueryExpressionVisitor<TArgument, TResult> visitor, TArgument argument)
        {
            return visitor.VisitComparison(this, argument);
        }

        public override string ToString()
        {
            return $"{Operator.ToString().Camelize()}({Left},{Right})";
        }
    }
}
