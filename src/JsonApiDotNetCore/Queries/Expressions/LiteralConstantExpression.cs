using System;

namespace JsonApiDotNetCore.Queries.Expressions
{
    /// <summary>
    /// Represents a non-null constant value, resulting from text such as: equals(firstName,'Jack')
    /// </summary>
    public class LiteralConstantExpression : IdentifierExpression
    {
        public string Value { get; }

        public LiteralConstantExpression(string text)
        {
            Value = text ?? throw new ArgumentNullException(nameof(text));
        }

        public override TResult Accept<TArgument, TResult>(QueryExpressionVisitor<TArgument, TResult> visitor, TArgument argument)
        {
            return visitor.VisitLiteralConstant(this, argument);
        }

        public override string ToString()
        {
            string value = Value.Replace("\'", "\'\'");
            return $"'{value}'";
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj is null || GetType() != obj.GetType())
            {
                return false;
            }

            var other = (LiteralConstantExpression) obj;

            return Value == other.Value;
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }
    }
}
