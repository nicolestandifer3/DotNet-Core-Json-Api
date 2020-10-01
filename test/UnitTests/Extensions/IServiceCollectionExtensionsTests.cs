using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Errors;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Repositories;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Serialization;
using JsonApiDotNetCore.Serialization.Building;
using JsonApiDotNetCore.Services;
using JsonApiDotNetCoreExample.Data;
using JsonApiDotNetCoreExample.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace UnitTests.Extensions
{
    public sealed class IServiceCollectionExtensionsTests
    {
        [Fact]
        public void AddJsonApiInternals_Adds_All_Required_Services()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<ISystemClock, FrozenSystemClock>();
            services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase("UnitTestDb"));
            services.AddJsonApi<AppDbContext>();

            // Act
            // this is required because the DbContextResolver requires access to the current HttpContext
            // to get the request scoped DbContext instance
            services.AddScoped<IRequestScopedServiceProvider, TestScopedServiceProvider>();
            var provider = services.BuildServiceProvider();

            // Assert
            var request = provider.GetRequiredService<IJsonApiRequest>() as JsonApiRequest;
            Assert.NotNull(request);
            var resourceGraph = provider.GetService<IResourceGraph>();
            Assert.NotNull(resourceGraph);
            request.PrimaryResource = resourceGraph.GetResourceContext<TodoItem>();
            Assert.NotNull(provider.GetService<IResourceGraph>());
            Assert.NotNull(provider.GetService<IDbContextResolver>());
            Assert.NotNull(provider.GetService(typeof(IResourceRepository<TodoItem>)));
            Assert.NotNull(provider.GetService<IResourceGraph>());
            Assert.NotNull(provider.GetService<IHttpContextAccessor>());
            Assert.NotNull(provider.GetService<IMetaBuilder>());
            Assert.NotNull(provider.GetService<IJsonApiSerializerFactory>());
            Assert.NotNull(provider.GetService<IJsonApiWriter>());
            Assert.NotNull(provider.GetService<IJsonApiReader>());
            Assert.NotNull(provider.GetService<IJsonApiDeserializer>());
            Assert.NotNull(provider.GetService<IGenericServiceFactory>());
            Assert.NotNull(provider.GetService(typeof(RepositoryRelationshipUpdateHelper<TodoItem>)));
        }

        [Fact]
        public void RegisterResource_DeviatingDbContextPropertyName_RegistersCorrectly()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<ISystemClock, FrozenSystemClock>();
            services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase("UnitTestDb"));
            services.AddJsonApi<AppDbContext>();

            // Act
            // this is required because the DbContextResolver requires access to the current HttpContext
            // to get the request scoped DbContext instance
            services.AddScoped<IRequestScopedServiceProvider, TestScopedServiceProvider>();
            var provider = services.BuildServiceProvider();
            var graph = provider.GetRequiredService<IResourceGraph>();
            var resourceContext = graph.GetResourceContext<Author>();

            // Assert 
            Assert.Equal("authors", resourceContext.PublicName);
        }

        [Fact]
        public void AddResourceService_Registers_All_Shorthand_Service_Interfaces()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddResourceService<IntResourceService>();

            // Assert
            var provider = services.BuildServiceProvider();
            Assert.IsType<IntResourceService>(provider.GetRequiredService(typeof(IResourceService<IntResource>)));
            Assert.IsType<IntResourceService>(provider.GetRequiredService(typeof(IResourceCommandService<IntResource>)));
            Assert.IsType<IntResourceService>(provider.GetRequiredService(typeof(IResourceQueryService<IntResource>)));
            Assert.IsType<IntResourceService>(provider.GetRequiredService(typeof(IGetAllService<IntResource>)));
            Assert.IsType<IntResourceService>(provider.GetRequiredService(typeof(IGetByIdService<IntResource>)));
            Assert.IsType<IntResourceService>(provider.GetRequiredService(typeof(IGetSecondaryService<IntResource>)));
            Assert.IsType<IntResourceService>(provider.GetRequiredService(typeof(IGetRelationshipService<IntResource>)));
            Assert.IsType<IntResourceService>(provider.GetRequiredService(typeof(ICreateService<IntResource>)));
            Assert.IsType<IntResourceService>(provider.GetRequiredService(typeof(IUpdateService<IntResource>)));
            Assert.IsType<IntResourceService>(provider.GetRequiredService(typeof(IDeleteService<IntResource>)));
        }

        [Fact]
        public void AddResourceService_Registers_All_LongForm_Service_Interfaces()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddResourceService<GuidResourceService>();

            // Assert
            var provider = services.BuildServiceProvider();
            Assert.IsType<GuidResourceService>(provider.GetRequiredService(typeof(IResourceService<GuidResource, Guid>)));
            Assert.IsType<GuidResourceService>(provider.GetRequiredService(typeof(IResourceCommandService<GuidResource, Guid>)));
            Assert.IsType<GuidResourceService>(provider.GetRequiredService(typeof(IResourceQueryService<GuidResource, Guid>)));
            Assert.IsType<GuidResourceService>(provider.GetRequiredService(typeof(IGetAllService<GuidResource, Guid>)));
            Assert.IsType<GuidResourceService>(provider.GetRequiredService(typeof(IGetByIdService<GuidResource, Guid>)));
            Assert.IsType<GuidResourceService>(provider.GetRequiredService(typeof(IGetSecondaryService<GuidResource, Guid>)));
            Assert.IsType<GuidResourceService>(provider.GetRequiredService(typeof(IGetRelationshipService<GuidResource, Guid>)));
            Assert.IsType<GuidResourceService>(provider.GetRequiredService(typeof(ICreateService<GuidResource, Guid>)));
            Assert.IsType<GuidResourceService>(provider.GetRequiredService(typeof(IUpdateService<GuidResource, Guid>)));
            Assert.IsType<GuidResourceService>(provider.GetRequiredService(typeof(IDeleteService<GuidResource, Guid>)));
        }

        [Fact]
        public void AddResourceService_Throws_If_Type_Does_Not_Implement_Any_Interfaces()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act, assert
            Assert.Throws<InvalidConfigurationException>(() => services.AddResourceService<int>());
        }

        [Fact]
        public void AddJsonApi_With_Context_Uses_Resource_Type_Name_If_NoOtherSpecified()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<TestContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString()));

            services.AddScoped<IRequestScopedServiceProvider, TestScopedServiceProvider>();

            // Act
            services.AddJsonApi<TestContext>();

            // Assert
            var provider = services.BuildServiceProvider();
            var resourceGraph = provider.GetRequiredService<IResourceGraph>();
            var resource = resourceGraph.GetResourceContext(typeof(IntResource));
            Assert.Equal("intResources", resource.PublicName);
        }

        public sealed class IntResource : Identifiable { }
        public class GuidResource : Identifiable<Guid> { }

        private class IntResourceService : IResourceService<IntResource>
        {
            public Task<IntResource> CreateAsync(IntResource resource) => throw new NotImplementedException();
            public Task DeleteAsync(int id) => throw new NotImplementedException();
            public Task<IReadOnlyCollection<IntResource>> GetAsync() => throw new NotImplementedException();
            public Task<IntResource> GetAsync(int id) => throw new NotImplementedException();
            public Task<object> GetSecondaryAsync(int id, string relationshipName) => throw new NotImplementedException();
            public Task<IntResource> GetRelationshipAsync(int id, string relationshipName) => throw new NotImplementedException();
            public Task<IntResource> UpdateAsync(int id, IntResource requestResource) => throw new NotImplementedException();
            public Task UpdateRelationshipAsync(int id, string relationshipName, object relationships) => throw new NotImplementedException();
        }

        private class GuidResourceService : IResourceService<GuidResource, Guid>
        {
            public Task<GuidResource> CreateAsync(GuidResource resource) => throw new NotImplementedException();
            public Task DeleteAsync(Guid id) => throw new NotImplementedException();
            public Task<IReadOnlyCollection<GuidResource>> GetAsync() => throw new NotImplementedException();
            public Task<GuidResource> GetAsync(Guid id) => throw new NotImplementedException();
            public Task<object> GetSecondaryAsync(Guid id, string relationshipName) => throw new NotImplementedException();
            public Task<GuidResource> GetRelationshipAsync(Guid id, string relationshipName) => throw new NotImplementedException();
            public Task<GuidResource> UpdateAsync(Guid id, GuidResource requestResource) => throw new NotImplementedException();
            public Task UpdateRelationshipAsync(Guid id, string relationshipName, object relationships) => throw new NotImplementedException();
        }


        public class TestContext : DbContext
        {
            public TestContext(DbContextOptions<TestContext> options) : base(options)
            {
            }

            public DbSet<IntResource> Resource { get; set; }
        }
    }
}
