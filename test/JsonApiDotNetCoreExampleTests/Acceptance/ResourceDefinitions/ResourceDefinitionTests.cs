using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Bogus;
using JsonApiDotNetCore.Models;
using JsonApiDotNetCore.Serialization;
using JsonApiDotNetCoreExample.Data;
using JsonApiDotNetCoreExample.Models;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Xunit;

namespace JsonApiDotNetCoreExampleTests.Acceptance
{
    [Collection("WebHostCollection")]
    public class ResourceDefinitionTests
    {
        private TestFixture<TestStartup> _fixture;
        private AppDbContext _context;
        private Faker<User> _userFaker;
        private static readonly Faker<Article> _articleFaker = new Faker<Article>()
            .RuleFor(a => a.Name, f => f.Random.AlphaNumeric(10))
            .RuleFor(a => a.Author, f => new Author());

        private static readonly Faker<Tag> _tagFaker = new Faker<Tag>().RuleFor(a => a.Name, f => f.Random.AlphaNumeric(10));
        public ResourceDefinitionTests(TestFixture<TestStartup> fixture)
        {
            _fixture = fixture;
            _context = fixture.GetService<AppDbContext>();
            _userFaker = new Faker<User>()
                .RuleFor(u => u.Username, f => f.Internet.UserName())
                .RuleFor(u => u.Password, f => f.Internet.Password());
        }

        [Fact]
        public async Task Unauthorized_Article()
        {
            // Arrange
            var context = _fixture.GetService<AppDbContext>();
            await context.SaveChangesAsync();

            var article = _articleFaker.Generate();
            article.Name = "Classified";
            context.Articles.Add(article);
            await context.SaveChangesAsync();

            var route = $"/api/v1/articles/{article.Id}";

            var httpMethod = new HttpMethod("GET");
            var request = new HttpRequestMessage(httpMethod, route);


            // Act
            var response = await _fixture.Client.GetAsync(route);

            // Assert
            var body = await response.Content.ReadAsStringAsync();
            Assert.True(HttpStatusCode.Unauthorized == response.StatusCode, $"{route} returned {response.StatusCode} status code with payload: {body}");

        }

        [Fact]
        public async Task Article_Is_Hidden()
        {
            // Arrange
            var context = _fixture.GetService<AppDbContext>();
            await context.SaveChangesAsync();

            var articles = _articleFaker.Generate(3).ToList();
            string toBeExcluded = "This should be not be included";
            articles[0].Name = toBeExcluded;


            context.Articles.AddRange(articles);
            await context.SaveChangesAsync();

            var route = $"/api/v1/articles";

            var httpMethod = new HttpMethod("GET");
            var request = new HttpRequestMessage(httpMethod, route);


            // Act
            var response = await _fixture.Client.GetAsync(route);

            // Assert
            var body = await response.Content.ReadAsStringAsync();
            Assert.True(HttpStatusCode.OK == response.StatusCode, $"{route} returned {response.StatusCode} status code with payload: {body}");
            Assert.DoesNotContain(toBeExcluded, body);
        }

        [Fact]
        public async Task Tag_Is_Hidden()
        {
            // Arrange
            var context = _fixture.GetService<AppDbContext>();
            await context.SaveChangesAsync();

            var article = _articleFaker.Generate();
            var tags = _tagFaker.Generate(2);
            string toBeExcluded = "This should be not be included";
            tags[0].Name = toBeExcluded;

            var articleTags = new ArticleTag[]
            {
                new ArticleTag
                {
                    Article = article,
                    Tag = tags[0]
                },
                new ArticleTag
                {
                    Article = article,
                    Tag = tags[1]
                }
            };
            context.ArticleTags.AddRange(articleTags);
            await context.SaveChangesAsync();

            var route = $"/api/v1/articles?include=tags";

            var httpMethod = new HttpMethod("GET");
            var request = new HttpRequestMessage(httpMethod, route);


            // Act
            var response = await _fixture.Client.GetAsync(route);

            // Assert
            var body = await response.Content.ReadAsStringAsync();
            Assert.True(HttpStatusCode.OK == response.StatusCode, $"{route} returned {response.StatusCode} status code with payload: {body}");
            Assert.DoesNotContain(toBeExcluded, body);
        }


        [Fact]
        public async Task Password_Is_Not_Included_In_Response_Payload()
        {
            // Arrange
            var user = _userFaker.Generate();
            _context.Users.Add(user);
            _context.SaveChanges();

            var httpMethod = new HttpMethod("GET");
            var route = $"/api/v1/users/{user.Id}";
            var request = new HttpRequestMessage(httpMethod, route);

            // Act
            var response = await _fixture.Client.SendAsync(request);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            var document = JsonConvert.DeserializeObject<Document>(body);
            Assert.False(document.Data.Attributes.ContainsKey("password"));
        }

        [Fact]
        public async Task Can_Create_User_With_Password()
        {
            // Arrange
            var user = _userFaker.Generate();
            var content = new
            {
                data = new
                {
                    type = "users",
                    attributes = new Dictionary<string, object>()
                    {
                        { "username", user.Username },
                        { "password", user.Password },
                    }
                }
            };

            var httpMethod = new HttpMethod("POST");
            var route = $"/api/v1/users";

            var request = new HttpRequestMessage(httpMethod, route);
            request.Content = new StringContent(JsonConvert.SerializeObject(content));
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.api+json");

            // Act
            var response = await _fixture.Client.SendAsync(request);

            // Assert
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);

            // response assertions
            var body = await response.Content.ReadAsStringAsync();
            var deserializedBody = (User)_fixture.GetService<IJsonApiDeSerializer>().Deserialize(body);
            var document = JsonConvert.DeserializeObject<Document>(body);
            Assert.False(document.Data.Attributes.ContainsKey("password"));
            Assert.Equal(user.Username, document.Data.Attributes["username"]);

            // db assertions
            var dbUser = await _context.Users.FindAsync(deserializedBody.Id);
            Assert.Equal(user.Username, dbUser.Username);
            Assert.Equal(user.Password, dbUser.Password);
        }

        [Fact]
        public async Task Can_Update_User_Password()
        {
            // Arrange
            var user = _userFaker.Generate();
            _context.Users.Add(user);
            _context.SaveChanges();

            var newPassword = _userFaker.Generate().Password;

            var content = new
            {
                data = new
                {
                    type = "users",
                    id = user.Id,
                    attributes = new Dictionary<string, object>()
                    {
                        { "password", newPassword },
                    }
                }
            };

            var httpMethod = new HttpMethod("PATCH");
            var route = $"/api/v1/users/{user.Id}";

            var request = new HttpRequestMessage(httpMethod, route);
            request.Content = new StringContent(JsonConvert.SerializeObject(content));
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.api+json");

            // Act
            var response = await _fixture.Client.SendAsync(request);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            // response assertions
            var body = await response.Content.ReadAsStringAsync();
            var deserializedBody = (User)_fixture.GetService<IJsonApiDeSerializer>().Deserialize(body);
            var document = JsonConvert.DeserializeObject<Document>(body);
            Assert.False(document.Data.Attributes.ContainsKey("password"));
            Assert.Equal(user.Username, document.Data.Attributes["username"]);

            // db assertions
            var dbUser = _context.Users.AsNoTracking().Single(u => u.Id == user.Id);
            Assert.Equal(newPassword, dbUser.Password);
        }
    }
}
