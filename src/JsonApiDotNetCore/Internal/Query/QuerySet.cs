using System.Collections.Generic;
using System.Linq;
using JsonApiDotNetCore.Services;

namespace JsonApiDotNetCore.Internal.Query
{
    public class QuerySet<T>
    {
        IJsonApiContext _jsonApiContext;

        public QuerySet(IJsonApiContext jsonApiContext)
        {
            _jsonApiContext = jsonApiContext;
            BuildQuerySet();
        }

        public FilterQuery Filter { get; set; }
        public List<SortQuery> SortParameters { get; set; }

        private void BuildQuerySet()
        {
            foreach (var pair in _jsonApiContext.Query)
            {
                if (pair.Key.StartsWith("filter"))
                {
                    Filter = ParseFilterQuery(pair.Key, pair.Value);
                    continue;
                }

                if (pair.Key.StartsWith("sort"))
                {
                    SortParameters = ParseSortParameters(pair.Value);
                }
            }
        }

        private FilterQuery ParseFilterQuery(string key, string value)
        {
            // expected input = filter[id]=1
            var propertyName = key.Split('[', ']')[1];
            var attribute = GetAttribute(propertyName);

            if(attribute == null)
                return null;

            return new FilterQuery(attribute, value);
        }

        // sort=id,name
        // sort=-id
        private List<SortQuery> ParseSortParameters(string value)
        {
            var sortParameters = new List<SortQuery>();
            value.Split(',').ToList().ForEach(p =>
            {
                var direction = SortDirection.Ascending;
                if (p[0] == '-')
                {
                    direction = SortDirection.Descending;
                    p = p.Substring(1);
                }

                var attribute = GetAttribute(p);

                sortParameters.Add(new SortQuery(direction, attribute));
            });

            return sortParameters;
        }

        private AttrAttribute GetAttribute(string propertyName)
        {
            return _jsonApiContext.RequestEntity.Attributes
                .FirstOrDefault(attr => 
                    attr.InternalAttributeName.ToLower() == propertyName.ToLower()
            );
        }
    }
}