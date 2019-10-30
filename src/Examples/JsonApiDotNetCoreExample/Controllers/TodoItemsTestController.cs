using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Models;
using JsonApiDotNetCore.Services;
using JsonApiDotNetCoreExample.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace JsonApiDotNetCoreExample.Controllers
{
    public abstract class AbstractTodoItemsController<T> 
    : JsonApiController<T> where T : class, IIdentifiable<int>
    {
        protected AbstractTodoItemsController(
            IJsonApiOptions jsonApiOptions,
            IResourceService<T, int> service,
            ILoggerFactory loggerFactory)
            : base(jsonApiOptions, service, loggerFactory)
        { }
    }

    [Route("/abstract")]
    public class TodoItemsTestController : AbstractTodoItemsController<TodoItem>
    {
        public TodoItemsTestController(
            IJsonApiOptions jsonApiOptions,
            IResourceService<TodoItem> service,
            ILoggerFactory loggerFactory) 
            : base(jsonApiOptions, service, loggerFactory)
        { }
    }
}
