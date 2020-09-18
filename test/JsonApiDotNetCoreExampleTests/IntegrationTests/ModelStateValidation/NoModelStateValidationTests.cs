using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using JsonApiDotNetCore.Serialization.Objects;
using Newtonsoft.Json;
using Xunit;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.ModelStateValidation
{
    public sealed class NoModelStateValidationTests : IClassFixture<IntegrationTestContext<TestableStartup<ModelStateDbContext>, ModelStateDbContext>>
    {
        private readonly IntegrationTestContext<TestableStartup<ModelStateDbContext>, ModelStateDbContext> _testContext;

        public NoModelStateValidationTests(IntegrationTestContext<TestableStartup<ModelStateDbContext>, ModelStateDbContext> testContext)
        {
            _testContext = testContext;
        }

        [Fact]
        public async Task When_posting_resource_with_invalid_attribute_value_it_must_succeed()
        {
            // Arrange
            var content = new
            {
                data = new
                {
                    type = "systemDirectories",
                    attributes = new Dictionary<string, object>
                    {
                        ["name"] = "!@#$%^&*().-",
                        ["isCaseSensitive"] = "false"
                    }
                }
            };

            string requestBody = JsonConvert.SerializeObject(content);
            string route = "/systemDirectories";

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecutePostAsync<Document>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.Created);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Attributes["name"].Should().Be("!@#$%^&*().-");
        }

        [Fact]
        public async Task When_patching_resource_with_invalid_attribute_value_it_must_succeed()
        {
            // Arrange
            var directory = new SystemDirectory
            {
                Name = "Projects",
                IsCaseSensitive = false
            };

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Directories.Add(directory);
                await dbContext.SaveChangesAsync();
            });

            var content = new
            {
                data = new
                {
                    type = "systemDirectories",
                    id = directory.StringId,
                    attributes = new Dictionary<string, object>
                    {
                        ["name"] = "!@#$%^&*().-"
                    }
                }
            };

            string requestBody = JsonConvert.SerializeObject(content);
            string route = "/systemDirectories/" + directory.StringId;

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecutePatchAsync<Document>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.Data.Should().BeNull();
        }
    }
}
