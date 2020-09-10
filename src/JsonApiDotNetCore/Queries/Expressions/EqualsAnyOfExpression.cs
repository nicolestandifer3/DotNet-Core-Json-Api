using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JsonApiDotNetCore.Queries.Internal.Parsing;

namespace JsonApiDotNetCore.Queries.Expressions
{
    /// <summary>
    /// Represents the "any" filter function, resulting from text such as: any(name,'Jack','Joe')
    /// </summary>
    public class EqualsAnyOfExpression : FilterExpression
    {
        public ResourceFieldChainExpression TargetAttribute { get; }
        public IReadOnlyCollection<LiteralConstantExpression> Constants { get; }

        public EqualsAnyOfExpression(ResourceFieldChainExpression targetAttribute,
            IReadOnlyCollection<LiteralConstantExpression> constants)
        {
            TargetAttribute = targetAttribute ?? throw new ArgumentNullException(nameof(targetAttribute));
            Constants = constants ?? throw new ArgumentNullException(nameof(constants));
        }

        public override TResult Accept<TArgument, TResult>(QueryExpressionVisitor<TArgument, TResult> visitor, TArgument argument)
        {
            return visitor.VisitEqualsAnyOf(this, argument);
        }

        public override string ToString()
        {
            var builder = new StringBuilder();

            builder.Append(Keywords.Any);
            builder.Append('(');
            builder.Append(TargetAttribute);
            builder.Append(',');
            builder.Append(string.Join(",", Constants.Select(constant => constant.ToString())));
            builder.Append(')');

            return builder.ToString();
        }
    }
}
