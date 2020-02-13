using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Services;
using JsonApiDotNetCoreExample.Models;
using Microsoft.Extensions.Logging;

namespace JsonApiDotNetCoreExample.Controllers
{
    public class KebabCasedModelsController : JsonApiController<KebabCasedModel>
    {
        public KebabCasedModelsController(
            IJsonApiOptions jsonApiOptions,
            ILoggerFactory loggerFactory,
            IResourceService<KebabCasedModel> resourceService)
            : base(jsonApiOptions, loggerFactory, resourceService)
        { }
    }
}
