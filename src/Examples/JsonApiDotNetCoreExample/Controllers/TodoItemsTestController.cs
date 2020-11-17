using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Controllers.Annotations;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Serialization.Objects;
using JsonApiDotNetCore.Services;
using JsonApiDotNetCoreExample.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace JsonApiDotNetCoreExample.Controllers
{
    public abstract class AbstractTodoItemsController<T> 
    : BaseJsonApiController<T> where T : class, IIdentifiable<int>
    {
        protected AbstractTodoItemsController(
            IJsonApiOptions options,
            ILoggerFactory loggerFactory,
            IResourceService<T, int> service)
            : base(options, loggerFactory, service)
        { }
    }

    [DisableRoutingConvention]
    [Route("/abstract")]
    public class TodoItemsTestController : AbstractTodoItemsController<TodoItem>
    {
        public TodoItemsTestController(
            IJsonApiOptions options,
            ILoggerFactory loggerFactory,
            IResourceService<TodoItem> service)
            : base(options, loggerFactory, service)
        { }

        [HttpGet]
        public override async Task<IActionResult> GetAsync() => await base.GetAsync();

        [HttpGet("{id}")]
        public override async Task<IActionResult> GetAsync(int id) => await base.GetAsync(id);

        [HttpGet("{id}/{relationshipName}")]
        public override async Task<IActionResult> GetSecondaryAsync(int id, string relationshipName)
            => await base.GetSecondaryAsync(id, relationshipName);

        [HttpGet("{id}/relationships/{relationshipName}")]
        public override async Task<IActionResult> GetRelationshipAsync(int id, string relationshipName)
            => await base.GetRelationshipAsync(id, relationshipName);

        [HttpPost]
        public override async Task<IActionResult> PostAsync([FromBody] TodoItem resource)
        {
            await Task.Yield();

            return NotFound(new Error(HttpStatusCode.NotFound)
            {
                Title = "NotFound ActionResult with explicit error object."
            });
        }

        [HttpPost("{id}/relationships/{relationshipName}")]
        public override async Task<IActionResult> PostRelationshipAsync(
            int id, string relationshipName, [FromBody] ISet<IIdentifiable> secondaryResourceIds)
            => await base.PostRelationshipAsync(id, relationshipName, secondaryResourceIds);

        [HttpPatch("{id}")]
        public override async Task<IActionResult> PatchAsync(int id, [FromBody] TodoItem resource)
        {
            await Task.Yield();

            return Conflict("Something went wrong");
        }

        [HttpPatch("{id}/relationships/{relationshipName}")]
        public override async Task<IActionResult> PatchRelationshipAsync(
            int id, string relationshipName, [FromBody] object secondaryResourceIds)
            => await base.PatchRelationshipAsync(id, relationshipName, secondaryResourceIds);

        [HttpDelete("{id}")]
        public override async Task<IActionResult> DeleteAsync(int id)
        {
            await Task.Yield();

            return NotFound();
        }

        [HttpDelete("{id}/relationships/{relationshipName}")]
        public override async Task<IActionResult> DeleteRelationshipAsync(int id, string relationshipName, [FromBody] ISet<IIdentifiable> secondaryResourceIds)
            => await base.DeleteRelationshipAsync(id, relationshipName, secondaryResourceIds);
    }
}
