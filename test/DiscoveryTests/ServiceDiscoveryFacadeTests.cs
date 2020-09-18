using System.Collections.Generic;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Hooks.Internal;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Repositories;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Serialization.Building;
using JsonApiDotNetCore.Services;
using JsonApiDotNetCoreExample.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace DiscoveryTests
{
    public sealed class ServiceDiscoveryFacadeTests
    {
        private readonly IServiceCollection _services = new ServiceCollection();
        private readonly JsonApiOptions _options = new JsonApiOptions();
        private readonly ResourceGraphBuilder _resourceGraphBuilder;

        public ServiceDiscoveryFacadeTests()
        {
            var dbResolverMock = new Mock<IDbContextResolver>();
            dbResolverMock.Setup(m => m.GetContext()).Returns(new Mock<DbContext>().Object);
            _services.AddScoped(_ => dbResolverMock.Object);

            _services.AddSingleton<IJsonApiOptions>(_options);
            _services.AddSingleton<ILoggerFactory>(new LoggerFactory());
            _services.AddScoped(_ => new Mock<IJsonApiRequest>().Object);
            _services.AddScoped(_ => new Mock<ITargetedFields>().Object);
            _services.AddScoped(_ => new Mock<IResourceGraph>().Object);
            _services.AddScoped(_ => new Mock<IGenericServiceFactory>().Object);
            _services.AddScoped(_ => new Mock<IResourceContextProvider>().Object);
            _services.AddScoped(typeof(IResourceChangeTracker<>), typeof(ResourceChangeTracker<>));
            _services.AddScoped(_ => new Mock<IResourceFactory>().Object);
            _services.AddScoped(_ => new Mock<IPaginationContext>().Object);
            _services.AddScoped(_ => new Mock<IQueryLayerComposer>().Object);

            _resourceGraphBuilder = new ResourceGraphBuilder(_options, NullLoggerFactory.Instance);
        }

        [Fact]
        public void DiscoverResources_Adds_Resources_From_Added_Assembly_To_Graph()
        {
            // Arrange
            ServiceDiscoveryFacade facade = new ServiceDiscoveryFacade(_services, _resourceGraphBuilder, _options, NullLoggerFactory.Instance);
            facade.AddAssembly(typeof(Person).Assembly);

            // Act
            facade.DiscoverResources();
            
            // Assert
            var resourceGraph = _resourceGraphBuilder.Build();
            var personResource = resourceGraph.GetResourceContext(typeof(Person));
            var articleResource = resourceGraph.GetResourceContext(typeof(Article));
            Assert.NotNull(personResource);
            Assert.NotNull(articleResource);
        }

        [Fact]
        public void DiscoverResources_Adds_Resources_From_Current_Assembly_To_Graph()
        {
            // Arrange
            ServiceDiscoveryFacade facade = new ServiceDiscoveryFacade(_services, _resourceGraphBuilder, _options, NullLoggerFactory.Instance);
            facade.AddCurrentAssembly();

            // Act
            facade.DiscoverResources();
            
            // Assert
            var resourceGraph = _resourceGraphBuilder.Build();
            var testModelResource = resourceGraph.GetResourceContext(typeof(TestModel));
            Assert.NotNull(testModelResource);
        }

        [Fact]
        public void DiscoverInjectables_Adds_Resource_Services_From_Current_Assembly_To_Container()
        {
            // Arrange
            ServiceDiscoveryFacade facade = new ServiceDiscoveryFacade(_services, _resourceGraphBuilder, _options, NullLoggerFactory.Instance);
            facade.AddCurrentAssembly();
            
            // Act
            facade.DiscoverInjectables();

            // Assert
            var services = _services.BuildServiceProvider();
            var service = services.GetService<IResourceService<TestModel>>();
            Assert.IsType<TestModelService>(service);
        }

        [Fact]
        public void DiscoverInjectables_Adds_Resource_Repositories_From_Current_Assembly_To_Container()
        {
            // Arrange
            ServiceDiscoveryFacade facade = new ServiceDiscoveryFacade(_services, _resourceGraphBuilder, _options, NullLoggerFactory.Instance);
            facade.AddCurrentAssembly();

            // Act
            facade.DiscoverInjectables();

            // Assert
            var services = _services.BuildServiceProvider();
            Assert.IsType<TestModelRepository>(services.GetService<IResourceRepository<TestModel>>());
        }

        [Fact]
        public void AddCurrentAssembly_Adds_Resource_Definitions_From_Current_Assembly_To_Container()
        {
            // Arrange
            ServiceDiscoveryFacade facade = new ServiceDiscoveryFacade(_services, _resourceGraphBuilder, _options, NullLoggerFactory.Instance);
            facade.AddCurrentAssembly();

            // Act
            facade.DiscoverInjectables();

            // Assert
            var services = _services.BuildServiceProvider();
            Assert.IsType<TestModelResourceDefinition>(services.GetService<IResourceDefinition<TestModel>>());
        }

        [Fact]
        public void AddCurrentAssembly_Adds_Resource_Hooks_Definitions_From_Current_Assembly_To_Container()
        {
            // Arrange
            ServiceDiscoveryFacade facade = new ServiceDiscoveryFacade(_services, _resourceGraphBuilder, _options, NullLoggerFactory.Instance);
            facade.AddCurrentAssembly();

            _options.EnableResourceHooks = true;

            // Act
            facade.DiscoverInjectables();

            // Assert
            var services = _services.BuildServiceProvider();
            Assert.IsType<TestModelResourceHooksDefinition>(services.GetService<ResourceHooksDefinition<TestModel>>());
        }

        public sealed class TestModel : Identifiable { }

        public class TestModelService : JsonApiResourceService<TestModel>
        {
            public TestModelService(
                IResourceRepository<TestModel> repository,
                IQueryLayerComposer queryLayerComposer,
                IPaginationContext paginationContext,
                IJsonApiOptions options,
                ILoggerFactory loggerFactory,
                IJsonApiRequest request,
                IResourceChangeTracker<TestModel> resourceChangeTracker,
                IResourceFactory resourceFactory,
                IResourceHookExecutor hookExecutor = null)
                : base(repository, queryLayerComposer, paginationContext, options, loggerFactory, request,
                    resourceChangeTracker, resourceFactory, hookExecutor)
            {
            }
        }

        public class TestModelRepository : EntityFrameworkCoreRepository<TestModel>
        {
            public TestModelRepository(
                ITargetedFields targetedFields,
                IDbContextResolver contextResolver,
                IResourceGraph resourceGraph,
                IGenericServiceFactory genericServiceFactory,
                IResourceFactory resourceFactory,
                IEnumerable<IQueryConstraintProvider> constraintProviders,
                ILoggerFactory loggerFactory)
                : base(targetedFields, contextResolver, resourceGraph, genericServiceFactory, resourceFactory, constraintProviders, loggerFactory)
            { }
        }
        
        public class TestModelResourceHooksDefinition : ResourceHooksDefinition<TestModel>
        {
            public TestModelResourceHooksDefinition(IResourceGraph resourceGraph) : base(resourceGraph) { }
        }

        public class TestModelResourceDefinition : JsonApiResourceDefinition<TestModel>
        {
            public TestModelResourceDefinition(IResourceGraph resourceGraph) : base(resourceGraph) { }
        }
    }
}
