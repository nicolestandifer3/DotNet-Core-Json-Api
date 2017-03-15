using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Bogus;
using DotNetCoreDocs;
using DotNetCoreDocs.Writers;
using JsonApiDotNetCore.Serialization;
using JsonApiDotNetCore.Services;
using JsonApiDotNetCoreExample;
using JsonApiDotNetCoreExample.Data;
using JsonApiDotNetCoreExample.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Newtonsoft.Json;
using Xunit;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace JsonApiDotNetCoreExampleTests.Acceptance.Spec
{
    [Collection("WebHostCollection")]
    public class CreatingDataTests
    {
        private DocsFixture<Startup, JsonDocWriter> _fixture;
        private IJsonApiContext _jsonApiContext;
        private Faker<TodoItem> _todoItemFaker;

        public CreatingDataTests(DocsFixture<Startup, JsonDocWriter> fixture)
        {
            _fixture = fixture;
            _jsonApiContext = fixture.GetService<IJsonApiContext>();
            _todoItemFaker = new Faker<TodoItem>()
                .RuleFor(t => t.Description, f => f.Lorem.Sentence())
                .RuleFor(t => t.Ordinal, f => f.Random.Number());
        }

        [Fact]
        public async Task Can_Create_Guid_Identifiable_Entities()
        {
            // arrange
            var builder = new WebHostBuilder()
                .UseStartup<Startup>();
            var httpMethod = new HttpMethod("POST");
            var server = new TestServer(builder);
            var client = server.CreateClient();

            var context = _fixture.GetService<AppDbContext>();

            var owner = new JsonApiDotNetCoreExample.Models.Person();
            context.People.Add(owner);
            await context.SaveChangesAsync();

            var route = "/api/v1/todo-item-collections";
            var request = new HttpRequestMessage(httpMethod, route);
            var content = new
            {
                data = new
                {
                    type = "todo-item-collections",
                    relationships = new
                    {
                        owner = new
                        {
                            data = new
                            {
                                type = "people",
                                id = owner.Id.ToString()
                            }
                        }
                    }
                }
            };
            request.Content = new StringContent(JsonConvert.SerializeObject(content));
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.api+json");
            
            // act
            var response = await client.SendAsync(request);

            // assert
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        [Fact]
        public async Task Request_With_ClientGeneratedId_Returns_403()
        {
            // arrange
            var builder = new WebHostBuilder()
                .UseStartup<Startup>();
            var httpMethod = new HttpMethod("POST");
            var route = "/api/v1/todo-items";
            var server = new TestServer(builder);
            var client = server.CreateClient();
            var request = new HttpRequestMessage(httpMethod, route);
            var todoItem = _todoItemFaker.Generate();
            var content = new
            {
                data = new
                {
                    type = "todo-items",
                    id = "9999",
                    attributes = new
                    {
                        description = todoItem.Description,
                        ordinal = todoItem.Ordinal
                    }
                }
            };
            request.Content = new StringContent(JsonConvert.SerializeObject(content));
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.api+json");
            
            // act
            var response = await client.SendAsync(request);

            // assert
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task Can_Create_And_Set_HasMany_Relationships()
        {
            // arrange
            var builder = new WebHostBuilder()
                .UseStartup<Startup>();
            var httpMethod = new HttpMethod("POST");
            var server = new TestServer(builder);
            var client = server.CreateClient();

            var context = _fixture.GetService<AppDbContext>();

            var owner = new JsonApiDotNetCoreExample.Models.Person();
            var todoItem = new TodoItem();
            todoItem.Owner = owner;
            context.People.Add(owner);
            context.TodoItems.Add(todoItem);
            await context.SaveChangesAsync();

            var route = "/api/v1/todo-item-collections";
            var request = new HttpRequestMessage(httpMethod, route);
            var content = new
            {
                data = new
                {
                    type = "todo-item-collections",
                    relationships = new Dictionary<string, dynamic>
                    {
                        {  "owner",  new {
                            data = new
                            {
                                type = "people",
                                id = owner.Id.ToString()
                            }
                        } },
                        {  "todo-items", new {
                            data = new dynamic[]
                            {
                                new {
                                    type = "todo-items",
                                    id = todoItem.Id.ToString()
                                }
                            }
                        } }
                    }
                }
            };

            request.Content = new StringContent(JsonConvert.SerializeObject(content));
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.api+json");
            
            // act
            var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            var deserializedBody = (TodoItemCollection)JsonApiDeSerializer.Deserialize(body, _jsonApiContext, context);
            var newId = deserializedBody.Id;
            var contextCollection = context.TodoItemCollections
                .Include(c=> c.Owner)
                .Include(c => c.TodoItems)
                .SingleOrDefault(c => c.Id == newId);

            // assert
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            Assert.Equal(owner.Id, contextCollection.OwnerId);
            Assert.NotEmpty(contextCollection.TodoItems);
        }

        [Fact]
        public async Task ShouldReceiveLocationHeader_InResponse()
        {
            // arrange
            var builder = new WebHostBuilder()
                .UseStartup<Startup>();
            var httpMethod = new HttpMethod("POST");
            var route = "/api/v1/todo-items";
            var server = new TestServer(builder);
            var client = server.CreateClient();
            var request = new HttpRequestMessage(httpMethod, route);
            var todoItem = _todoItemFaker.Generate();
            var content = new
            {
                data = new
                {
                    type = "todo-items",
                    attributes = new
                    {
                        description = todoItem.Description,
                        ordinal = todoItem.Ordinal
                    }
                }
            };
            request.Content = new StringContent(JsonConvert.SerializeObject(content));
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.api+json");
            
            // act
            var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            var deserializedBody = (TodoItem)JsonApiDeSerializer.Deserialize(body, _jsonApiContext, _fixture.GetService<AppDbContext>());

            // assert
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            Assert.Equal($"/api/v1/todo-items/{deserializedBody.Id}", response.Headers.Location.ToString());
        }

        [Fact]
        public async Task Respond_409_ToIncorrectEntityType()
        {
            // arrange
            var builder = new WebHostBuilder()
                .UseStartup<Startup>();
            var httpMethod = new HttpMethod("POST");
            var route = "/api/v1/todo-items";
            var server = new TestServer(builder);
            var client = server.CreateClient();
            var request = new HttpRequestMessage(httpMethod, route);
            var todoItem = _todoItemFaker.Generate();
            var content = new
            {
                data = new
                {
                    type = "people",
                    attributes = new
                    {
                        description = todoItem.Description,
                        ordinal = todoItem.Ordinal
                    }
                }
            };
            request.Content = new StringContent(JsonConvert.SerializeObject(content));
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.api+json");
            
            // act
            var response = await client.SendAsync(request);

            // assert
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }
    }
}
