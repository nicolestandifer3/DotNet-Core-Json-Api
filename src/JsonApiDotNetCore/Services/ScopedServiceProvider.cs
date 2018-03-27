using Microsoft.AspNetCore.Http;
using System;

namespace JsonApiDotNetCore.Services
{
    /// <summary>
    /// An interface used to separate the registration of the global ServiceProvider
    /// from a request scoped service provider. This is useful in cases when we need to 
    /// manually resolve services from the request scope (e.g. operation processors)
    /// </summary>
    public interface IScopedServiceProvider : IServiceProvider { }

    /// <summary>
    /// A service provider that uses the current HttpContext request scope
    /// </summary>
    public class RequestScopedServiceProvider : IScopedServiceProvider
    {
        private readonly IServiceProvider _serviceProvider;

        public RequestScopedServiceProvider(IHttpContextAccessor httpContextAccessor)
        {
            _serviceProvider = httpContextAccessor.HttpContext.RequestServices;
        }

        /// <inheritdoc />
        public object GetService(Type serviceType) => _serviceProvider.GetService(serviceType);
    }
}
