using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;

namespace JsonApiDotNetCoreTests.IntegrationTests.QueryStrings.Filtering
{
    public sealed class FilterableResourcesController : JsonApiController<FilterableResource, int>
    {
        public FilterableResourcesController(IJsonApiOptions options, ILoggerFactory loggerFactory, IResourceService<FilterableResource, int> resourceService)
            : base(options, loggerFactory, resourceService)
        {
        }
    }
}
