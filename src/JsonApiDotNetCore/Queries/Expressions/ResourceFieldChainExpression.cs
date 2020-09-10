using System;
using System.Collections.Generic;
using System.Linq;
using JsonApiDotNetCore.Resources.Annotations;

namespace JsonApiDotNetCore.Queries.Expressions
{
    /// <summary>
    /// Represents a chain of fields (relationships and attributes), resulting from text such as: articles.revisions.author
    /// </summary>
    public class ResourceFieldChainExpression : IdentifierExpression
    {
        public IReadOnlyCollection<ResourceFieldAttribute> Fields { get; }

        public ResourceFieldChainExpression(ResourceFieldAttribute field)
        {
            if (field == null)
            {
                throw new ArgumentNullException(nameof(field));
            }

            Fields = new[] {field};
        }

        public ResourceFieldChainExpression(IReadOnlyCollection<ResourceFieldAttribute> fields)
        {
            Fields = fields ?? throw new ArgumentNullException(nameof(fields));

            if (!fields.Any())
            {
                throw new ArgumentException("Must have one or more fields.", nameof(fields));
            }
        }

        public override TResult Accept<TArgument, TResult>(QueryExpressionVisitor<TArgument, TResult> visitor,
            TArgument argument)
        {
            return visitor.VisitResourceFieldChain(this, argument);
        }

        public override string ToString()
        {
            return string.Join(".", Fields.Select(field => field.PublicName));
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

            var other = (ResourceFieldChainExpression) obj;

            return Fields.SequenceEqual(other.Fields);
        }

        public override int GetHashCode()
        {
            var hashCode = new HashCode();

            foreach (var field in Fields)
            {
                hashCode.Add(field);
            }

            return hashCode.ToHashCode();
        }
    }
}
