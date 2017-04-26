﻿using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using DotNetCoreDocs;
using DotNetCoreDocs.Writers;
using JsonApiDotNetCoreExample;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Xunit;
using JsonApiDotNetCoreExample.Data;
using Bogus;
using JsonApiDotNetCoreExample.Models;
using JsonApiDotNetCore.Serialization;
using System.Linq;
using Person = JsonApiDotNetCoreExample.Models.Person;
using Newtonsoft.Json;
using JsonApiDotNetCore.Models;

namespace JsonApiDotNetCoreExampleTests.Acceptance.Spec
{
    [Collection("WebHostCollection")]
    public class AttributeFilterTests
    {
        private DocsFixture<Startup, JsonDocWriter> _fixture;
        private Faker<TodoItem> _todoItemFaker;
        private readonly Faker<Person> _personFaker;

        public AttributeFilterTests(DocsFixture<Startup, JsonDocWriter> fixture)
        {
            _fixture = fixture;
            _todoItemFaker = new Faker<TodoItem>()
                .RuleFor(t => t.Description, f => f.Lorem.Sentence())
                .RuleFor(t => t.Ordinal, f => f.Random.Number())
                .RuleFor(t => t.CreatedDate, f => f.Date.Past());

            _personFaker = new Faker<Person>()
                .RuleFor(p => p.FirstName, f => f.Name.FirstName())
                .RuleFor(p => p.LastName, f => f.Name.LastName());
        }

        [Fact]
        public async Task Can_Filter_On_Guid_Properties()
        {
            // arrange
            var context = _fixture.GetService<AppDbContext>();
            var todoItem = _todoItemFaker.Generate();
            context.TodoItems.Add(todoItem);
            await context.SaveChangesAsync();
            
            var builder = new WebHostBuilder()
                .UseStartup<Startup>();
            var httpMethod = new HttpMethod("GET");
            var route = $"/api/v1/todo-items?filter[guid-property]={todoItem.GuidProperty}";
            var server = new TestServer(builder);
            var client = server.CreateClient();
            var request = new HttpRequestMessage(httpMethod, route);

            // act
            var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            var deserializedBody = _fixture
                .GetService<IJsonApiDeSerializer>()
                .DeserializeList<TodoItem>(body);

            var todoItemResponse = deserializedBody.Single();

            // assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(todoItem.Id, todoItemResponse.Id);
            Assert.Equal(todoItem.GuidProperty, todoItemResponse.GuidProperty);
        }


        [Fact]
        public async Task Can_Filter_On_Related_Attrs()
        {
            // arrange
            var context = _fixture.GetService<AppDbContext>();
            var person = _personFaker.Generate();
            var todoItem = _todoItemFaker.Generate();
            todoItem.Owner = person;
            context.TodoItems.Add(todoItem);
            await context.SaveChangesAsync();
            
            var builder = new WebHostBuilder()
                .UseStartup<Startup>();
            var httpMethod = new HttpMethod("GET");
            var route = $"/api/v1/todo-items?include=owner&filter[owner.first-name]={person.FirstName}";
            var server = new TestServer(builder);
            var client = server.CreateClient();
            var request = new HttpRequestMessage(httpMethod, route);

            // act
            var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            var documents = JsonConvert.DeserializeObject<Documents>(await response.Content.ReadAsStringAsync());
            var included = documents.Included;

            // assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotNull(included);
            Assert.NotEmpty(included);
            foreach(var item in included)
                Assert.Equal(person.FirstName, item.Attributes["first-name"]);
        }
    }
}
