using System;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCoreExample.Models;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace UnitTests.Internal
{
    public sealed class RequestScopedServiceProviderTests
    {
        [Fact]
        public void When_http_context_is_unavailable_it_must_fail()
        {
            // Arrange
            var serviceType = typeof(IIdentifiable<Tag>);

            var provider = new RequestScopedServiceProvider(new HttpContextAccessor());

            // Act
            Action action = () => provider.GetService(serviceType);

            // Assert
            var exception = Assert.Throws<InvalidOperationException>(action);

            Assert.StartsWith("Cannot resolve scoped service " + $"'{serviceType.FullName}' outside the context of an HTTP request.", exception.Message);
        }
    }
}
