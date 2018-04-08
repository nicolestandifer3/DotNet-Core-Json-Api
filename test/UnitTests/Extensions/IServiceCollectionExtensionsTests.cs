using JsonApiDotNetCore.Builders;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Extensions;
using JsonApiDotNetCore.Formatters;
using JsonApiDotNetCore.Internal;
using JsonApiDotNetCore.Internal.Generics;
using JsonApiDotNetCore.Serialization;
using JsonApiDotNetCore.Services;
using JsonApiDotNetCoreExample.Data;
using JsonApiDotNetCoreExample.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Microsoft.EntityFrameworkCore;

namespace UnitTests.Extensions
{
    public class IServiceCollectionExtensionsTests
    {
        [Fact]
        public void AddJsonApiInternals_Adds_All_Required_Services()
        {
            // arrange
            var services = new ServiceCollection();
            var jsonApiOptions = new JsonApiOptions();

            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseInMemoryDatabase();
            }, ServiceLifetime.Transient);

            // act
            services.AddJsonApiInternals<AppDbContext>(jsonApiOptions);
            // this is required because the DbContextResolver requires access to the current HttpContext
            // to get the request scoped DbContext instance
            services.AddScoped<IScopedServiceProvider, TestScopedServiceProvider>();
            var provider = services.BuildServiceProvider();

            // assert
            Assert.NotNull(provider.GetService<IDbContextResolver>());
            Assert.NotNull(provider.GetService(typeof(IEntityRepository<TodoItem>)));
            Assert.NotNull(provider.GetService<JsonApiOptions>());
            Assert.NotNull(provider.GetService<IContextGraph>());
            Assert.NotNull(provider.GetService<IJsonApiContext>());
            Assert.NotNull(provider.GetService<IHttpContextAccessor>());
            Assert.NotNull(provider.GetService<IMetaBuilder>());
            Assert.NotNull(provider.GetService<IDocumentBuilder>());
            Assert.NotNull(provider.GetService<IJsonApiSerializer>());
            Assert.NotNull(provider.GetService<IJsonApiWriter>());
            Assert.NotNull(provider.GetService<IJsonApiReader>());
            Assert.NotNull(provider.GetService<IJsonApiDeSerializer>());
            Assert.NotNull(provider.GetService<IGenericProcessorFactory>());
            Assert.NotNull(provider.GetService<IDocumentBuilderOptionsProvider>());
            Assert.NotNull(provider.GetService(typeof(GenericProcessor<TodoItem>)));
        }
    }
}
