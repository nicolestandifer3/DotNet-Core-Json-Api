using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using JsonApiDotNetCore.Configuration;
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
            }

            if (await IsValidAsync())
            {
                await _next(httpContext);
            }
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
            var ns = $"/{GetNameSpace()}";
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
                return GetNameSpace();
            }
            var ns = GetNameSpace();
            var customRoute = GetCustomRoute(r.Path.Value, resourceName);
            var toReturn = $"{r.Scheme}://{r.Host}/{ns}";
            if (customRoute != null)
            {
                toReturn += $"/{customRoute}";
            }
            return toReturn;
        }

        private object GetCustomRoute(string path, string resourceName)
        {
            var ns = GetNameSpace();
            var trimmedComponents = path.Trim('/').Split('/').ToList();
            var resourceNameIndex = trimmedComponents.FindIndex(c => c == resourceName);
            var newComponents = trimmedComponents.Take(resourceNameIndex).ToArray();
            var customRoute = string.Join('/', newComponents);
            if (customRoute == ns)
            {
                return null;
            }
            else
            {
                return customRoute;
            }
        }

        private string GetNameSpace()
        {
            return _options.Namespace;
        }

        private bool PathIsRelationship()
        {
            var actionName = (string)_routeValues["action"];
            return actionName.ToLower().Contains("relationships");
        }

        private async Task<bool> IsValidAsync()
        {
            return await IsValidContentTypeHeaderAsync(_httpContext) && await IsValidAcceptHeaderAsync(_httpContext);
        }

        private static async Task<bool> IsValidContentTypeHeaderAsync(HttpContext context)
        {
            var contentType = context.Request.ContentType;
            if (contentType != null && ContainsMediaTypeParameters(contentType))
            {
                await FlushResponseAsync(context, new Error(HttpStatusCode.UnsupportedMediaType)
                {
                    Title = "The specified Content-Type header value is not supported.",
                    Detail = $"Please specify '{Constants.ContentType}' for the Content-Type header value."
                });

                return false;
            }
            return true;
        }

        private static async Task<bool> IsValidAcceptHeaderAsync(HttpContext context)
        {
            if (context.Request.Headers.TryGetValue(Constants.AcceptHeader, out StringValues acceptHeaders) == false)
                return true;

            foreach (var acceptHeader in acceptHeaders)
            {
                if (ContainsMediaTypeParameters(acceptHeader) == false)
                {
                    continue;
                }

                await FlushResponseAsync(context, new Error(HttpStatusCode.NotAcceptable)
                {
                    Title = "The specified Accept header value is not supported.",
                    Detail = $"Please specify '{Constants.ContentType}' for the Accept header value."
                });
                return false;
            }
            return true;
        }

        private static bool ContainsMediaTypeParameters(string mediaType)
        {
            var incomingMediaTypeSpan = mediaType.AsSpan();

            // if the content type is not application/vnd.api+json then continue on
            if (incomingMediaTypeSpan.Length < Constants.ContentType.Length)
            {
                return false;
            }

            var incomingContentType = incomingMediaTypeSpan.Slice(0, Constants.ContentType.Length);
            if (incomingContentType.SequenceEqual(Constants.ContentType.AsSpan()) == false)
                return false;

            // anything appended to "application/vnd.api+json;" will be considered a media type param
            return (
                incomingMediaTypeSpan.Length >= Constants.ContentType.Length + 2
                && incomingMediaTypeSpan[Constants.ContentType.Length] == ';'
            );
        }

        private static async Task FlushResponseAsync(HttpContext context, Error error)
        {
            context.Response.StatusCode = (int) error.StatusCode;

            string responseBody = JsonConvert.SerializeObject(new ErrorDocument(error));
            await using (var writer = new StreamWriter(context.Response.Body))
            {
                await writer.WriteAsync(responseBody);
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
