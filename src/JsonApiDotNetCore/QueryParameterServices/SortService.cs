using System.Collections.Generic;
using System.Linq;
using System.Net;
using JsonApiDotNetCore.Internal;
using JsonApiDotNetCore.Internal.Contracts;
using JsonApiDotNetCore.Internal.Query;
using JsonApiDotNetCore.Managers.Contracts;
using Microsoft.Extensions.Primitives;

namespace JsonApiDotNetCore.Query
{
    /// <inheritdoc/>
    public class SortService : QueryParameterService, ISortService
    {
        private const char DESCENDING_SORT_OPERATOR = '-';
        private readonly IResourceDefinitionProvider _resourceDefinitionProvider;
        private List<SortQueryContext> _queries;

        public SortService(IResourceDefinitionProvider resourceDefinitionProvider,
                           IResourceGraph resourceGraph,
                           ICurrentRequest currentRequest)
            : base(resourceGraph, currentRequest)
        {
            _resourceDefinitionProvider = resourceDefinitionProvider;
            _queries = new List<SortQueryContext>();
        }

        /// <inheritdoc/>
        public virtual void Parse(KeyValuePair<string, StringValues> queryParameter)
        {
            EnsureNoNestedResourceRoute();
            var queries = BuildQueries(queryParameter.Value);

            _queries = queries.Select(BuildQueryContext).ToList();
        }

        /// <inheritdoc/>
        public List<SortQueryContext> Get()
        {
            if (_queries == null)
            {
                var requestResourceDefinition = _resourceDefinitionProvider.Get(_requestResource.ResourceType);
                if (requestResourceDefinition != null)
                    return requestResourceDefinition.DefaultSort()?.Select(d => BuildQueryContext(new SortQuery(d.Item1.PublicAttributeName, d.Item2))).ToList();
            }
            return _queries.ToList();
        }

        private List<SortQuery> BuildQueries(string value)
        {
            var sortParameters = new List<SortQuery>();

            var sortSegments = value.Split(QueryConstants.COMMA);
            if (sortSegments.Any(s => s == string.Empty))
                throw new JsonApiException(HttpStatusCode.BadRequest, "The sort URI segment contained a null value.");

            foreach (var sortSegment in sortSegments)
            {
                var propertyName = sortSegment;
                var direction = SortDirection.Ascending;

                if (sortSegment[0] == DESCENDING_SORT_OPERATOR)
                {
                    direction = SortDirection.Descending;
                    propertyName = propertyName.Substring(1);
                }

                sortParameters.Add(new SortQuery(propertyName, direction));
            }

            return sortParameters;
        }

        private SortQueryContext BuildQueryContext(SortQuery query)
        {
            var relationship = GetRelationship(query.Relationship);
            var attribute = GetAttribute(query.Attribute, relationship);

            if (attribute.IsSortable == false)
                throw new JsonApiException(HttpStatusCode.BadRequest, $"Sort is not allowed for attribute '{attribute.PublicAttributeName}'.");

            return new SortQueryContext(query)
            {
                Attribute = attribute,
                Relationship = relationship
            };
        }
    }
}
