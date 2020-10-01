using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries.Expressions;

namespace JsonApiDotNetCore.Resources
{
    /// <summary>
    /// Provides a resource-centric extensibility point for executing custom code when something happens with a resource.
    /// The goal here is to reduce the need for overriding the service and repository layers.
    /// </summary>
    /// <typeparam name="TResource">The resource type.</typeparam>
    public class JsonApiResourceDefinition<TResource> : JsonApiResourceDefinition<TResource, int>, IResourceDefinition<TResource>
        where TResource : class, IIdentifiable<int>
    {
        public JsonApiResourceDefinition(IResourceGraph resourceGraph) 
            : base(resourceGraph)
        {
        }
    }

    /// <inheritdoc />
    public class JsonApiResourceDefinition<TResource, TId> : IResourceDefinition<TResource, TId>
        where TResource : class, IIdentifiable<TId>
    {
        protected IResourceGraph ResourceGraph { get; }

        public JsonApiResourceDefinition(IResourceGraph resourceGraph)
        {
            ResourceGraph = resourceGraph ?? throw new ArgumentNullException(nameof(resourceGraph));
        }

        /// <inheritdoc />
        public virtual IReadOnlyCollection<IncludeElementExpression> OnApplyIncludes(IReadOnlyCollection<IncludeElementExpression> existingIncludes)
        {
            return existingIncludes;
        }

        /// <inheritdoc />
        public virtual FilterExpression OnApplyFilter(FilterExpression existingFilter)
        {
            return existingFilter;
        }

        /// <inheritdoc />
        public virtual SortExpression OnApplySort(SortExpression existingSort)
        {
            return existingSort;
        }

        /// <summary>
        /// Creates a <see cref="SortExpression"/> from a lambda expression.
        /// </summary>
        /// <example>
        /// <code><![CDATA[
        /// var sort = CreateSortExpressionFromLambda(new PropertySortOrder
        /// {
        ///     (model => model.CreatedAt, ListSortDirection.Ascending),
        ///     (model => model.Password, ListSortDirection.Descending)
        /// });
        /// ]]></code>
        /// </example>
        protected SortExpression CreateSortExpressionFromLambda(PropertySortOrder keySelectors)
        {
            if (keySelectors == null)
            {
                throw new ArgumentNullException(nameof(keySelectors));
            }

            List<SortElementExpression> sortElements = new List<SortElementExpression>();

            foreach (var (keySelector, sortDirection) in keySelectors)
            {
                bool isAscending = sortDirection == ListSortDirection.Ascending;
                var attribute = ResourceGraph.GetAttributes(keySelector).Single();

                var sortElement = new SortElementExpression(new ResourceFieldChainExpression(attribute), isAscending);
                sortElements.Add(sortElement);
            }

            return new SortExpression(sortElements);
        }

        /// <inheritdoc />
        public virtual PaginationExpression OnApplyPagination(PaginationExpression existingPagination)
        {
            return existingPagination;
        }

        /// <inheritdoc />
        public virtual SparseFieldSetExpression OnApplySparseFieldSet(SparseFieldSetExpression existingSparseFieldSet)
        {
            return existingSparseFieldSet;
        }

        /// <inheritdoc />
        public virtual QueryStringParameterHandlers<TResource> OnRegisterQueryableHandlersForQueryStringParameters()
        {
            return null;
        }

        /// <inheritdoc />
        public virtual IDictionary<string, object> GetMeta(TResource resource)
        {
            return null;
        }

        /// <summary>
        /// This is an alias type intended to simplify the implementation's method signature.
        /// See <see cref="CreateSortExpressionFromLambda"/> for usage details.
        /// </summary>
        public sealed class PropertySortOrder : List<(Expression<Func<TResource, dynamic>> KeySelector, ListSortDirection SortDirection)>
        {
        }
    }
}
