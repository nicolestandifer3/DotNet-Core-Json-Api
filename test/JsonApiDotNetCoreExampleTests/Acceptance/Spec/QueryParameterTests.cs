using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using JsonApiDotNetCore.Models.JsonApiDocuments;
using JsonApiDotNetCoreExample;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Newtonsoft.Json;
using Xunit;

namespace JsonApiDotNetCoreExampleTests.Acceptance.Spec
{
    [Collection("WebHostCollection")]
    public sealed class QueryParameterTests
    {
        [Fact]
        public async Task Server_Returns_400_ForUnknownQueryParam()
        {
            // Arrange
            const string queryString = "?someKey=someValue";

            var builder = new WebHostBuilder().UseStartup<Startup>();
            var server = new TestServer(builder);
            var client = server.CreateClient();
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/todoItems" + queryString);

            // Act
            var response = await client.SendAsync(request);
            
            // Assert
            var body = await response.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            
            var errorDocument = JsonConvert.DeserializeObject<ErrorDocument>(body);
            Assert.Single(errorDocument.Errors);
            Assert.Equal(HttpStatusCode.BadRequest, errorDocument.Errors[0].StatusCode);
            Assert.Equal("Unknown query string parameter.", errorDocument.Errors[0].Title);
            Assert.Equal("Query string parameter 'someKey' is unknown. Set 'AllowCustomQueryStringParameters' to 'true' in options to ignore unknown parameters.", errorDocument.Errors[0].Detail);
            Assert.Equal("someKey", errorDocument.Errors[0].Source.Parameter);
        }

        [Fact]
        public async Task Server_Returns_400_ForMissingQueryParameterValue()
        {
            // Arrange
            const string queryString = "?include=";

            var builder = new WebHostBuilder().UseStartup<Startup>();
            var httpMethod = new HttpMethod("GET");
            var route = $"/api/v1/todoItems" + queryString;
            var server = new TestServer(builder);
            var client = server.CreateClient();
            var request = new HttpRequestMessage(httpMethod, route);

            // Act
            var response = await client.SendAsync(request);

            // Assert
            var body = await response.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

            var errorDocument = JsonConvert.DeserializeObject<ErrorDocument>(body);
            Assert.Single(errorDocument.Errors);
            Assert.Equal(HttpStatusCode.BadRequest, errorDocument.Errors[0].StatusCode);
            Assert.Equal("Missing query string parameter value.", errorDocument.Errors[0].Title);
            Assert.Equal("Missing value for 'include' query string parameter.", errorDocument.Errors[0].Detail);
            Assert.Equal("include", errorDocument.Errors[0].Source.Parameter);
        }
    }
}
