using System;
using System.Collections.Generic;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Internal;
using JsonApiDotNetCore.Internal.Query;
using Microsoft.Extensions.Primitives;

namespace JsonApiDotNetCore.Query
{
    /// <inheritdoc/>
    public class PageService : QueryParameterService, IPageService
    {
        private IJsonApiOptions _options;

        public PageService(IJsonApiOptions options)
        {
            _options = options;
            DefaultPageSize = _options.DefaultPageSize;
            PageSize = _options.DefaultPageSize;
        }
        /// <inheritdoc/>
        public int? TotalRecords { get; set; }
        /// <inheritdoc/>
        public int PageSize { get; set; }
        /// <inheritdoc/>
        public int DefaultPageSize { get; set; } // I think we shouldnt expose this
        /// <inheritdoc/>
        public int CurrentPage { get; set; }
        /// <inheritdoc/>
        public int TotalPages => (TotalRecords == null) ? -1 : (int)Math.Ceiling(decimal.Divide(TotalRecords.Value, PageSize));

        /// <inheritdoc/>
        public virtual void Parse(KeyValuePair<string, StringValues> queryParameter)
        {
            // expected input = page[size]=10
            //                  page[number]=1
            var propertyName = queryParameter.Key.Split(QueryConstants.OPEN_BRACKET, QueryConstants.CLOSE_BRACKET)[1];

            const string SIZE = "size";
            const string NUMBER = "number";

            if (propertyName == SIZE)
            {
                if (int.TryParse(queryParameter.Value, out var size))
                    PageSize = size;
                else 
                    throw new JsonApiException(400, $"Invalid page size '{queryParameter.Value}'");
            }
            else if (propertyName == NUMBER)
            {
                if (int.TryParse(queryParameter.Value, out var size))
                    CurrentPage = size;
                else
                    throw new JsonApiException(400, $"Invalid page number '{queryParameter.Value}'");
            }
        }

        /// <inheritdoc/>
        public bool ShouldPaginate()
        {
            return (PageSize > 0) || ((CurrentPage == 1 || CurrentPage == 0) && TotalPages <= 0);
        }
    }
}
