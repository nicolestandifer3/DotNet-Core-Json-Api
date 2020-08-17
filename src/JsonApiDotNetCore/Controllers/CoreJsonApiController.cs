using System.Collections.Generic;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Models.JsonApiDocuments;
using Microsoft.AspNetCore.Mvc;

namespace JsonApiDotNetCore.Controllers
{
    [ServiceFilter(typeof(IQueryStringActionFilter))]
    public abstract class CoreJsonApiController : ControllerBase
    {
        protected IActionResult Error(Error error)
        {
            return Error(new[] {error});
        }

        protected IActionResult Error(IEnumerable<Error> errors)
        {
            var document = new ErrorDocument(errors);

            return new ObjectResult(document)
            {
                StatusCode = (int) document.GetErrorStatusCode()
            };
        }
    }
}
