using System;
using System.Collections.Generic;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Internal;
using JsonApiDotNetCore.Internal.Contracts;
using JsonApiDotNetCore.Internal.Query;
using JsonApiDotNetCore.Managers.Contracts;
using Microsoft.Extensions.Primitives;

namespace JsonApiDotNetCore.Query
{
    /// <inheritdoc/>
    public class PageService : QueryParameterService, IPageService
    {
        private readonly IJsonApiOptions _options;
        public PageService(IJsonApiOptions options, IResourceGraph resourceGraph, ICurrentRequest currentRequest) : base(resourceGraph, currentRequest)
        {
            _options = options;
            DefaultPageSize = _options.DefaultPageSize;
        }

        /// <summary>
        /// constructor used for unit testing
        /// </summary>
        internal PageService(IJsonApiOptions options)
        {
            _options = options;
            DefaultPageSize = _options.DefaultPageSize;
        }

        /// <inheritdoc/>
        public int PageSize
        {
            get
            {
                if (RequestedPageSize.HasValue)
                {
                    return RequestedPageSize.Value;
                }
                return DefaultPageSize;
            }
        }

        /// <inheritdoc/>
        public int DefaultPageSize { get; set; }

        /// <inheritdoc/>
        public int? RequestedPageSize { get; set; }

        /// <inheritdoc/>
        public int CurrentPage { get; set; } = 1;

        /// <inheritdoc/>
        public bool Backwards { get; set; }

        /// <inheritdoc/>
        public int TotalPages => (TotalRecords == null || PageSize == 0) ? -1 : (int)Math.Ceiling(decimal.Divide(TotalRecords.Value, PageSize));

        /// <inheritdoc/>
        public bool CanPaginate => TotalPages > 1;

        /// <inheritdoc/>
        public int? TotalRecords { get; set; }

        /// <inheritdoc/>
        public virtual void Parse(KeyValuePair<string, StringValues> queryParameter)
        {
            EnsureNoNestedResourceRoute();
            // expected input = page[size]=<integer>
            //                  page[number]=<integer greater than zero> 
            var propertyName = queryParameter.Key.Split(QueryConstants.OPEN_BRACKET, QueryConstants.CLOSE_BRACKET)[1];

            const string SIZE = "size";
            const string NUMBER = "number";

            if (propertyName == SIZE)
            {
                if (!int.TryParse(queryParameter.Value, out var size))
                {
                    ThrowBadPagingRequest(queryParameter, "value could not be parsed as an integer");
                }
                else if (size < 1)
                {
                    ThrowBadPagingRequest(queryParameter, "value needs to be greater than zero");
                }
                else if (size > _options.MaximumPageSize)
                {
                    ThrowBadPagingRequest(queryParameter, $"page size cannot be higher than {_options.MaximumPageSize}.");
                }
                else
                {
                    RequestedPageSize = size;
                }
            }
            else if (propertyName == NUMBER)
            { 
                if (!int.TryParse(queryParameter.Value, out var number))
                {
                    ThrowBadPagingRequest(queryParameter, "value could not be parsed as an integer");
                }
                else if (number == 0)
                {
                    ThrowBadPagingRequest(queryParameter, "page index is not zero-based");
                }
                else if (number > _options.MaximumPageNumber)
                {
                    ThrowBadPagingRequest(queryParameter, $"page index cannot be higher than {_options.MaximumPageNumber}.");
                }
                else
                {
                    Backwards = (number < 0);
                    CurrentPage = Math.Abs(number);
                }
            }
        }

        private void ThrowBadPagingRequest(KeyValuePair<string, StringValues> parameter, string message)
        {
            throw new JsonApiException(400, $"Invalid page query parameter '{parameter.Key}={parameter.Value}': {message}");
        }

    }
}
