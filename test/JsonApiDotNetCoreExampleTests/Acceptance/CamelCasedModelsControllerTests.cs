﻿using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Bogus;
using DotNetCoreDocs;
using DotNetCoreDocs.Writers;
using JsonApiDotNetCoreExample;
using JsonApiDotNetCoreExample.Data;
using JsonApiDotNetCoreExample.Models;
using Newtonsoft.Json;
using Xunit;
using JsonApiDotNetCore.Services;
using JsonApiDotNetCore.Serialization;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Hosting;

namespace JsonApiDotNetCoreExampleTests.Acceptance
{
    [Collection("WebHostCollection")]
    public class CamelCasedModelsControllerTests
    {
        private DocsFixture<Startup, JsonDocWriter> _fixture;
        private AppDbContext _context;
        private IJsonApiContext _jsonApiContext;
        private Faker<CamelCasedModel> _faker;

        public CamelCasedModelsControllerTests(DocsFixture<Startup, JsonDocWriter> fixture)
        {
            _fixture = fixture;
            _context = fixture.GetService<AppDbContext>();
            _jsonApiContext = fixture.GetService<IJsonApiContext>();
            _faker = new Faker<CamelCasedModel>()
                .RuleFor(m => m.CompoundAttr, f => f.Lorem.Sentence());
        }

        [Fact]
        public async Task Can_Get_CamelCasedModels()
        {
            // Arrange
            var model = _faker.Generate();
            _context.CamelCasedModels.Add(model);
            _context.SaveChanges();

            var httpMethod = new HttpMethod("GET");
            var route = "/camelCasedModels";
            var builder = new WebHostBuilder()
                .UseStartup<Startup>();
            var server = new TestServer(builder);
            var client = server.CreateClient();
            var request = new HttpRequestMessage(httpMethod, route);

            // Act
            var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            var deserializedBody = _fixture.GetService<IJsonApiDeSerializer>()
                .DeserializeList<CamelCasedModel>(body);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotEmpty(deserializedBody);
            Assert.True(deserializedBody.Count >  0);
        }

        [Fact]
        public async Task Can_Get_CamelCasedModels_ById()
        {
            // Arrange
            var model = _faker.Generate();
            _context.CamelCasedModels.Add(model);
            _context.SaveChanges();

            var httpMethod = new HttpMethod("GET");
            var route = $"/camelCasedModels/{model.Id}";
            var builder = new WebHostBuilder()
                .UseStartup<Startup>();
            var server = new TestServer(builder);
            var client = server.CreateClient();
            var request = new HttpRequestMessage(httpMethod, route);

            // Act
            var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            var deserializedBody = (CamelCasedModel)_fixture.GetService<IJsonApiDeSerializer>()
                .Deserialize(body);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotNull(deserializedBody);
            Assert.Equal(model.Id, deserializedBody.Id);
        }

        [Fact]
        public async Task Can_Post_CamelCasedModels()
        {
            // Arrange
            var model = _faker.Generate();
            var content = new
            {
                data = new
                {
                    type = "camelCasedModels",
                    attributes = new Dictionary<string, object>()
                    {
                        { "compoundAttr", model.CompoundAttr }
                    }
                }
            };
            var httpMethod = new HttpMethod("POST");
            var route = $"/camelCasedModels";
            var builder = new WebHostBuilder()
                .UseStartup<Startup>();
            var server = new TestServer(builder);
            var client = server.CreateClient();
            var request = new HttpRequestMessage(httpMethod, route);
            request.Content = new StringContent(JsonConvert.SerializeObject(content));
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.api+json");
            
            // Act
            var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            Assert.NotNull(body);
            Assert.NotEmpty(body);

            var deserializedBody = (CamelCasedModel)_fixture.GetService<IJsonApiDeSerializer>()
                .Deserialize(body);
            Assert.Equal(model.CompoundAttr, deserializedBody.CompoundAttr);
        }

        [Fact]
        public async Task Can_Patch_CamelCasedModels()
        {
            // Arrange
            var model = _faker.Generate();
            _context.CamelCasedModels.Add(model);
            _context.SaveChanges();

            var newModel = _faker.Generate();
            var content = new
            {
                data = new
                {
                    type = "camelCasedModels",
                    attributes = new Dictionary<string, object>()
                    {
                        { "compoundAttr", newModel.CompoundAttr }
                    }
                }
            };
            var httpMethod = new HttpMethod("PATCH");
            var route = $"/camelCasedModels/{model.Id}";
            var builder = new WebHostBuilder()
                .UseStartup<Startup>();
            var server = new TestServer(builder);
            var client = server.CreateClient();
            var request = new HttpRequestMessage(httpMethod, route);
            request.Content = new StringContent(JsonConvert.SerializeObject(content));
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.api+json");
            
            // Act
            var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotNull(body);
            Assert.NotEmpty(body);

            var deserializedBody = (CamelCasedModel)_fixture.GetService<IJsonApiDeSerializer>()
                .Deserialize(body);
            Assert.Equal(newModel.CompoundAttr, deserializedBody.CompoundAttr);
        }
    }
}