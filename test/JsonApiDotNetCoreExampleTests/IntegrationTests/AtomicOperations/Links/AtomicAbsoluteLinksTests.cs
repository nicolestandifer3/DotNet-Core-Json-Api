using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Serialization.Objects;
using JsonApiDotNetCoreExampleTests.Startups;
using Microsoft.Extensions.DependencyInjection;
using TestBuildingBlocks;
using Xunit;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.AtomicOperations.Links
{
    public sealed class AtomicAbsoluteLinksTests
        : IClassFixture<ExampleIntegrationTestContext<TestableStartup<OperationsDbContext>, OperationsDbContext>>
    {
        private const string HostPrefix = "http://localhost";

        private readonly ExampleIntegrationTestContext<TestableStartup<OperationsDbContext>, OperationsDbContext> _testContext;
        private readonly OperationsFakers _fakers = new OperationsFakers();

        public AtomicAbsoluteLinksTests(ExampleIntegrationTestContext<TestableStartup<OperationsDbContext>, OperationsDbContext> testContext)
        {
            _testContext = testContext;

            testContext.ConfigureServicesAfterStartup(services =>
            {
                services.AddControllersFromExampleProject();

                services.AddScoped(typeof(IResourceChangeTracker<>), typeof(NeverSameResourceChangeTracker<>));
            });
        }

        [Fact]
        public async Task Update_resource_with_side_effects_returns_absolute_links()
        {
            // Arrange
            var existingLanguage = _fakers.TextLanguage.Generate();
            var existingCompany = _fakers.RecordCompany.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.AddRange(existingLanguage, existingCompany);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                atomic__operations = new object[]
                {
                    new
                    {
                        op = "update",
                        data = new
                        {
                            type = "textLanguages",
                            id = existingLanguage.StringId,
                            attributes = new
                            {
                            }
                        }
                    },
                    new
                    {
                        op = "update",
                        data = new
                        {
                            type = "recordCompanies",
                            id = existingCompany.StringId,
                            attributes = new
                            {
                            }
                        }
                    }
                }
            };

            const string route = "/operations";

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecutePostAtomicAsync<AtomicOperationsDocument>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.Results.Should().HaveCount(2);

            string languageLink = HostPrefix + "/textLanguages/" + existingLanguage.StringId;

            var singleData1 = responseDocument.Results[0].SingleData;
            singleData1.Should().NotBeNull();
            singleData1.Links.Should().NotBeNull();
            singleData1.Links.Self.Should().Be(languageLink);
            singleData1.Relationships.Should().NotBeEmpty();
            singleData1.Relationships["lyrics"].Links.Should().NotBeNull();
            singleData1.Relationships["lyrics"].Links.Self.Should().Be(languageLink + "/relationships/lyrics");
            singleData1.Relationships["lyrics"].Links.Related.Should().Be(languageLink + "/lyrics");

            string companyLink = HostPrefix + "/recordCompanies/" + existingCompany.StringId;

            var singleData2 = responseDocument.Results[1].SingleData;
            singleData2.Should().NotBeNull();
            singleData2.Links.Should().NotBeNull();
            singleData2.Links.Self.Should().Be(companyLink);
            singleData2.Relationships.Should().NotBeEmpty();
            singleData2.Relationships["tracks"].Links.Should().NotBeNull();
            singleData2.Relationships["tracks"].Links.Self.Should().Be(companyLink + "/relationships/tracks");
            singleData2.Relationships["tracks"].Links.Related.Should().Be(companyLink + "/tracks");
        }
    }
}
