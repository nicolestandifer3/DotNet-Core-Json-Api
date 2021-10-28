using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;

namespace JsonApiDotNetCoreTests.IntegrationTests.ContentNegotiation
{
    public sealed class PoliciesController : JsonApiController<Policy, int>
    {
        public PoliciesController(IJsonApiOptions options, ILoggerFactory loggerFactory, IResourceService<Policy, int> resourceService)
            : base(options, loggerFactory, resourceService)
        {
        }
    }
}
