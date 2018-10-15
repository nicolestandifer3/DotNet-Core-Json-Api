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
using JsonApiDotNetCore.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

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

            services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase("UnitTestDb"), ServiceLifetime.Transient);

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
            Assert.NotNull(provider.GetService<IResourceGraph>());
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

        [Fact]
        public void AddResourceService_Registers_All_Shorthand_Service_Interfaces()
        {
            // arrange
            var services = new ServiceCollection();

            // act
            services.AddResourceService<IntResourceService>();
            
            // assert
            var provider = services.BuildServiceProvider();
            Assert.IsType<IntResourceService>(provider.GetService(typeof(IResourceService<IntResource>)));
            Assert.IsType<IntResourceService>(provider.GetService(typeof(IResourceCmdService<IntResource>)));
            Assert.IsType<IntResourceService>(provider.GetService(typeof(IResourceQueryService<IntResource>)));
            Assert.IsType<IntResourceService>(provider.GetService(typeof(IGetAllService<IntResource>)));
            Assert.IsType<IntResourceService>(provider.GetService(typeof(IGetByIdService<IntResource>)));
            Assert.IsType<IntResourceService>(provider.GetService(typeof(IGetRelationshipService<IntResource>)));
            Assert.IsType<IntResourceService>(provider.GetService(typeof(IGetRelationshipsService<IntResource>)));
            Assert.IsType<IntResourceService>(provider.GetService(typeof(ICreateService<IntResource>)));
            Assert.IsType<IntResourceService>(provider.GetService(typeof(IUpdateService<IntResource>)));
            Assert.IsType<IntResourceService>(provider.GetService(typeof(IDeleteService<IntResource>)));
        }

        [Fact]
        public void AddResourceService_Registers_All_LongForm_Service_Interfaces()
        {
            // arrange
            var services = new ServiceCollection();

            // act
            services.AddResourceService<GuidResourceService>();
            
            // assert
            var provider = services.BuildServiceProvider();
            Assert.IsType<GuidResourceService>(provider.GetService(typeof(IResourceService<GuidResource, Guid>)));
            Assert.IsType<GuidResourceService>(provider.GetService(typeof(IResourceCmdService<GuidResource, Guid>)));
            Assert.IsType<GuidResourceService>(provider.GetService(typeof(IResourceQueryService<GuidResource, Guid>)));
            Assert.IsType<GuidResourceService>(provider.GetService(typeof(IGetAllService<GuidResource, Guid>)));
            Assert.IsType<GuidResourceService>(provider.GetService(typeof(IGetByIdService<GuidResource, Guid>)));
            Assert.IsType<GuidResourceService>(provider.GetService(typeof(IGetRelationshipService<GuidResource, Guid>)));
            Assert.IsType<GuidResourceService>(provider.GetService(typeof(IGetRelationshipsService<GuidResource, Guid>)));
            Assert.IsType<GuidResourceService>(provider.GetService(typeof(ICreateService<GuidResource, Guid>)));
            Assert.IsType<GuidResourceService>(provider.GetService(typeof(IUpdateService<GuidResource, Guid>)));
            Assert.IsType<GuidResourceService>(provider.GetService(typeof(IDeleteService<GuidResource, Guid>)));
        }

        [Fact]
        public void AddResourceService_Throws_If_Type_Does_Not_Implement_Any_Interfaces()
        {
            // arrange
            var services = new ServiceCollection();
            
            // act, assert
            Assert.Throws<JsonApiSetupException>(() => services.AddResourceService<int>());
        }

        [Fact]
        public void AddJsonApi_With_Context_Uses_DbSet_PropertyName_If_NoOtherSpecified()
        {
            // arrange
            var services = new ServiceCollection();

            services.AddScoped<IScopedServiceProvider, TestScopedServiceProvider>();

            // act
            services.AddJsonApi<TestContext>();

            // assert
            var provider = services.BuildServiceProvider();
            var graph = provider.GetService<IResourceGraph>();
            var resource = graph.GetContextEntity(typeof(IntResource));
            Assert.Equal("resource", resource.EntityName);
        }

        public class IntResource : Identifiable { }
        public class GuidResource : Identifiable<Guid> { }

        private class IntResourceService : IResourceService<IntResource>
        {
            public Task<IntResource> CreateAsync(IntResource entity) => throw new NotImplementedException();
            public Task<bool> DeleteAsync(int id) => throw new NotImplementedException();
            public Task<IEnumerable<IntResource>> GetAsync() => throw new NotImplementedException();
            public Task<IntResource> GetAsync(int id) => throw new NotImplementedException();
            public Task<object> GetRelationshipAsync(int id, string relationshipName) => throw new NotImplementedException();
            public Task<object> GetRelationshipsAsync(int id, string relationshipName) => throw new NotImplementedException();
            public Task<IntResource> UpdateAsync(int id, IntResource entity) => throw new NotImplementedException();
            public Task UpdateRelationshipsAsync(int id, string relationshipName, List<ResourceObject> relationships) => throw new NotImplementedException();
        }

        private class GuidResourceService : IResourceService<GuidResource, Guid>
        {
            public Task<GuidResource> CreateAsync(GuidResource entity) => throw new NotImplementedException();
            public Task<bool> DeleteAsync(Guid id) => throw new NotImplementedException();
            public Task<IEnumerable<GuidResource>> GetAsync() => throw new NotImplementedException();
            public Task<GuidResource> GetAsync(Guid id) => throw new NotImplementedException();
            public Task<object> GetRelationshipAsync(Guid id, string relationshipName) => throw new NotImplementedException();
            public Task<object> GetRelationshipsAsync(Guid id, string relationshipName) => throw new NotImplementedException();
            public Task<GuidResource> UpdateAsync(Guid id, GuidResource entity) => throw new NotImplementedException();
            public Task UpdateRelationshipsAsync(Guid id, string relationshipName, List<ResourceObject> relationships) => throw new NotImplementedException();
        }


        public class TestContext : DbContext 
        {
            public DbSet<IntResource> Resource { get; set; }
        }
    }
}
