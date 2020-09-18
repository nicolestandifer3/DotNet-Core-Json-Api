using System.Linq;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries.Expressions;
using JsonApiDotNetCore.Resources;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.SoftDeletion
{
    public class SoftDeletionResourceDefinition<TResource> : JsonApiResourceDefinition<TResource>
        where TResource : class, IIdentifiable<int>, ISoftDeletable
    {
        private readonly IResourceGraph _resourceGraph;

        public SoftDeletionResourceDefinition(IResourceGraph resourceGraph)
            : base(resourceGraph)
        {
            _resourceGraph = resourceGraph;
        }

        public override FilterExpression OnApplyFilter(FilterExpression existingFilter)
        {
            var resourceContext = _resourceGraph.GetResourceContext<TResource>();
            var isSoftDeletedAttribute = resourceContext.Attributes.Single(attribute => attribute.Property.Name == nameof(ISoftDeletable.IsSoftDeleted));

            var isNotSoftDeleted = new ComparisonExpression(ComparisonOperator.Equals,
                new ResourceFieldChainExpression(isSoftDeletedAttribute), new LiteralConstantExpression("false"));

            return existingFilter == null
                ? (FilterExpression) isNotSoftDeleted
                : new LogicalExpression(LogicalOperator.And, new[] {isNotSoftDeleted, existingFilter});
        }

    }
}
