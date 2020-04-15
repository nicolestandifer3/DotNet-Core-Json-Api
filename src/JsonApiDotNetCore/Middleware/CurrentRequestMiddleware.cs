using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Extensions;
using JsonApiDotNetCore.Internal;
using JsonApiDotNetCore.Internal.Contracts;
using JsonApiDotNetCore.Managers.Contracts;
using JsonApiDotNetCore.Models.JsonApiDocuments;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;

namespace JsonApiDotNetCore.Middleware
{
    /// <summary>
    /// This sets all necessary parameters relating to the HttpContext for JADNC
    /// </summary>
    public sealed class CurrentRequestMiddleware
    {
        private readonly RequestDelegate _next;
        private HttpContext _httpContext;
        private ICurrentRequest _currentRequest;
        private IResourceGraph _resourceGraph;
        private IJsonApiOptions _options;
        private RouteValueDictionary _routeValues;
        private IControllerResourceMapping _controllerResourceMapping;

        public CurrentRequestMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext httpContext,
                                 IControllerResourceMapping controllerResourceMapping,
                                 IJsonApiOptions options,
                                 ICurrentRequest currentRequest,
                                 IResourceGraph resourceGraph)
        {
            _httpContext = httpContext;
            _currentRequest = currentRequest;
            _controllerResourceMapping = controllerResourceMapping;
            _resourceGraph = resourceGraph;
            _options = options;
            _routeValues = httpContext.GetRouteData().Values;
            var requestResource = GetCurrentEntity();
            if (requestResource != null)
            {
                _currentRequest.SetRequestResource(requestResource);
                _currentRequest.IsRelationshipPath = PathIsRelationship();
                _currentRequest.BasePath = GetBasePath(requestResource.ResourceName);
                _currentRequest.BaseId = GetBaseId();
                _currentRequest.RelationshipId = GetRelationshipId();

                if (await IsValidAsync())
                {
                    _httpContext.SetJsonApiRequest();
                    await _next(httpContext);
                }

                return;
            }

            await _next(httpContext);
        }

        private string GetBaseId()
        {
            if (_routeValues.TryGetValue("id", out object stringId))
            {
                return (string)stringId;
            }

            return null;
        }
        private string GetRelationshipId()
        {
            if (!_currentRequest.IsRelationshipPath)
            {
                return null;
            }
            var components = SplitCurrentPath();
            var toReturn = components.ElementAtOrDefault(4);

            return toReturn;
        }
        private string[] SplitCurrentPath()
        {
            var path = _httpContext.Request.Path.Value;
            var ns = $"/{_options.Namespace}";
            var nonNameSpaced = path.Replace(ns, "");
            nonNameSpaced = nonNameSpaced.Trim('/');
            var individualComponents = nonNameSpaced.Split('/');
            return individualComponents;
        }

        private string GetBasePath(string resourceName = null)
        {
            var r = _httpContext.Request;
            if (_options.RelativeLinks)
            {
                return _options.Namespace;
            }

            var customRoute = GetCustomRoute(r.Path.Value, resourceName);
            var toReturn = $"{r.Scheme}://{r.Host}/{_options.Namespace}";
            if (customRoute != null)
            {
                toReturn += $"/{customRoute}";
            }
            return toReturn;
        }

        private object GetCustomRoute(string path, string resourceName)
        {
            var trimmedComponents = path.Trim('/').Split('/').ToList();
            var resourceNameIndex = trimmedComponents.FindIndex(c => c == resourceName);
            var newComponents = trimmedComponents.Take(resourceNameIndex).ToArray();
            var customRoute = string.Join('/', newComponents);
            if (customRoute == _options.Namespace)
            {
                return null;
            }
            else
            {
                return customRoute;
            }
        }

        private bool PathIsRelationship()
        {
            var actionName = (string)_routeValues["action"];
            return actionName.ToLowerInvariant().Contains("relationships");
        }

        private async Task<bool> IsValidAsync()
        {
            return await IsValidContentTypeHeaderAsync(_httpContext) && await IsValidAcceptHeaderAsync(_httpContext);
        }

        private async Task<bool> IsValidContentTypeHeaderAsync(HttpContext context)
        {
            var contentType = context.Request.ContentType;
            if (contentType != null)
            {
                if (!MediaTypeHeaderValue.TryParse(contentType, out var headerValue) ||
                    headerValue.MediaType != HeaderConstants.MediaType || headerValue.CharSet != null ||
                    headerValue.Parameters.Any(p => p.Name != "ext"))
                {
                    await FlushResponseAsync(context, new Error(HttpStatusCode.UnsupportedMediaType)
                    {
                        Title = "The specified Content-Type header value is not supported.",
                        Detail = $"Please specify '{HeaderConstants.MediaType}' instead of '{contentType}' for the Content-Type header value."
                    });

                    return false;
                }
            }

            return true;
        }

        private async Task<bool> IsValidAcceptHeaderAsync(HttpContext context)
        {
            if (context.Request.Headers.TryGetValue("Accept", out StringValues acceptHeaders))
            {
                foreach (var acceptHeader in acceptHeaders)
                {
                    if (MediaTypeHeaderValue.TryParse(acceptHeader, out var headerValue))
                    {
                        if (headerValue.MediaType == HeaderConstants.MediaType &&
                            headerValue.Parameters.All(p => p.Name == "ext"))
                        {
                            return true;
                        }
                    }
                }

                await FlushResponseAsync(context, new Error(HttpStatusCode.NotAcceptable)
                {
                    Title = "The specified Accept header value is not supported.",
                    Detail = $"Please include '{HeaderConstants.MediaType}' in the Accept header values."
                });
                return false;
            }

            return true;
        }

        private async Task FlushResponseAsync(HttpContext context, Error error)
        {
            context.Response.StatusCode = (int) error.StatusCode;

            JsonSerializer serializer = JsonSerializer.CreateDefault(_options.SerializerSettings);
            serializer.ApplyErrorSettings();

            // https://github.com/JamesNK/Newtonsoft.Json/issues/1193
            await using (var stream = new MemoryStream())
            {
                await using (var streamWriter = new StreamWriter(stream, leaveOpen: true))
                {
                    using var jsonWriter = new JsonTextWriter(streamWriter);
                    serializer.Serialize(jsonWriter, new ErrorDocument(error));
                }

                stream.Seek(0, SeekOrigin.Begin);
                await stream.CopyToAsync(context.Response.Body);
            }

            context.Response.Body.Flush();
        }

        /// <summary>
        /// Gets the current entity that we need for serialization and deserialization.
        /// </summary>
        /// <returns></returns>
        private ResourceContext GetCurrentEntity()
        {
            var controllerName = (string)_routeValues["controller"];
            if (controllerName == null)
            {
                return null;
            }
            var resourceType = _controllerResourceMapping.GetAssociatedResource(controllerName);
            var requestResource = _resourceGraph.GetResourceContext(resourceType);
            if (requestResource == null)
            {
                return null;
            }
            if (_routeValues.TryGetValue("relationshipName", out object relationshipName))
            {
                _currentRequest.RequestRelationship = requestResource.Relationships.SingleOrDefault(r => r.PublicRelationshipName == (string)relationshipName);
            }
            return requestResource;
        }
    }
}
