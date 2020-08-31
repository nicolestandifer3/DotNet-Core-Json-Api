using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Services;
using JsonApiDotNetCoreExample.Models;
using Microsoft.Extensions.Logging;

namespace JsonApiDotNetCoreExample.Controllers
{
    public sealed class UsersController : JsonApiController<User>
    {
        public UsersController(
            IJsonApiOptions options,
            ILoggerFactory loggerFactory,
            IResourceService<User> resourceService)
            : base(options, loggerFactory, resourceService)
        { }
    }

    public sealed class SuperUsersController : JsonApiController<SuperUser>
    {
        public SuperUsersController(
            IJsonApiOptions options,
            ILoggerFactory loggerFactory,
            IResourceService<SuperUser> resourceService)
            : base(options, loggerFactory, resourceService)
        { }
    }
}
