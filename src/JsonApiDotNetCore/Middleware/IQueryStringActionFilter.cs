using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Filters;

namespace JsonApiDotNetCore.Middleware
{
    /// <summary>
    /// Extensibility point for processing request query strings.
    /// </summary>
    public interface IQueryStringActionFilter
    {
        Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next);
    }
}
