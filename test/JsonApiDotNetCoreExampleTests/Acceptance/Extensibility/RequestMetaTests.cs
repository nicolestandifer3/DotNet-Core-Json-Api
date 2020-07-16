using System.Net;
using System.Threading.Tasks;
using Xunit;
using JsonApiDotNetCore.Models;
using System.Collections.Generic;
using FluentAssertions;
using JsonApiDotNetCore.Services;
using JsonApiDotNetCoreExample;
using JsonApiDotNetCoreExample.Data;
using Microsoft.Extensions.DependencyInjection;

namespace JsonApiDotNetCoreExampleTests.Acceptance.Extensibility
{
    public sealed class RequestMetaTests : IClassFixture<IntegrationTestContext<Startup, AppDbContext>>
    {
        private readonly IntegrationTestContext<Startup, AppDbContext> _testContext;

        public RequestMetaTests(IntegrationTestContext<Startup, AppDbContext> testContext)
        {
            _testContext = testContext;

            testContext.ConfigureServicesBeforeStartup(services =>
            {
                services.AddScoped<IRequestMeta, TestRequestMeta>();
            });
        }

        [Fact]
        public async Task Injecting_IRequestMeta_Adds_Meta_Data()
        {
            // Arrange
            var route = "/api/v1/people";

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.Meta.Should().NotBeNull();
            responseDocument.Meta.ContainsKey("request-meta").Should().BeTrue();
            responseDocument.Meta["request-meta"].Should().Be("request-meta-value");
        }
    }

    public sealed class TestRequestMeta : IRequestMeta
    {
        public Dictionary<string, object> GetMeta()
        {
            return new Dictionary<string, object>
            {
                {"request-meta", "request-meta-value"}
            };
        }
    }
}
