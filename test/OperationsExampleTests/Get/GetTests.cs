using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Bogus;
using JsonApiDotNetCore.Models.Operations;
using JsonApiDotNetCoreExample.Data;
using OperationsExampleTests.Factories;
using Xunit;

namespace OperationsExampleTests
{
    public class GetByIdTests : Fixture, IDisposable
    {
        private readonly Faker _faker = new Faker();

        [Fact]
        public async Task Can_Get_Authors()
        {
            // arrange
            var expectedCount = _faker.Random.Int(1, 10);
            var context = GetService<AppDbContext>();
            context.Articles.RemoveRange(context.Articles);
            context.Authors.RemoveRange(context.Authors);
            var authors = AuthorFactory.Get(expectedCount);
            context.AddRange(authors);
            context.SaveChanges();

            var content = new
            {
                operations = new[] {
                    new Dictionary<string, object> {
                        { "op", "get"},
                        { "ref",  new { type = "authors" } }
                    }
                }
            };

            // act
            var result = await PatchAsync<OperationsDocument>("api/bulk", content);

            // assert
            Assert.NotNull(result.response);
            Assert.NotNull(result.data);
            Assert.Equal(HttpStatusCode.OK, result.response.StatusCode);
            Assert.Single(result.data.Operations);
            Assert.Equal(expectedCount, result.data.Operations.Single().DataList.Count);
        }
    }
}
