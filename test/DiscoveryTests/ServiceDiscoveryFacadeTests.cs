using GettingStarted.Models;
using GettingStarted.ResourceDefinitionExample;
using JsonApiDotNetCore.Builders;
using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Graph;
using JsonApiDotNetCore.Models;
using JsonApiDotNetCore.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace DiscoveryTests
{
    public class ServiceDiscoveryFacadeTests
    {
        private readonly IServiceCollection _services = new ServiceCollection();
        private readonly ResourceGraphBuilder _graphBuilder = new ResourceGraphBuilder();

        public ServiceDiscoveryFacadeTests()
        {
            var contextMock = new Mock<DbContext>();
            var dbResolverMock = new Mock<IDbContextResolver>();
            dbResolverMock.Setup(m => m.GetContext()).Returns(new Mock<DbContext>().Object);
            TestModelRepository._dbContextResolver = dbResolverMock.Object;
        }

        private ServiceDiscoveryFacade _facade => new ServiceDiscoveryFacade(_services, _graphBuilder);

        [Fact]
        public void AddAssembly_Adds_All_Resources_To_Graph()
        {
            // arrange, act
            _facade.AddAssembly(typeof(Person).Assembly);

            // assert
            var graph = _graphBuilder.Build();
            var personResource = graph.GetContextEntity(typeof(Person));
            var articleResource = graph.GetContextEntity(typeof(Article));
            var modelResource = graph.GetContextEntity(typeof(Model));

            Assert.NotNull(personResource);
            Assert.NotNull(articleResource);
            Assert.NotNull(modelResource);
        }

        [Fact]
        public void AddCurrentAssembly_Adds_Resources_To_Graph()
        {
            // arrange, act
            _facade.AddCurrentAssembly();

            // assert
            var graph = _graphBuilder.Build();
            var testModelResource = graph.GetContextEntity(typeof(TestModel));
            Assert.NotNull(testModelResource);
        }

        [Fact]
        public void AddCurrentAssembly_Adds_Services_To_Container()
        {
            // arrange, act
            _facade.AddCurrentAssembly();

            // assert
            var services = _services.BuildServiceProvider();
            Assert.IsType<TestModelService>(services.GetService<IResourceService<TestModel>>());
        }

        [Fact]
        public void AddCurrentAssembly_Adds_Repositories_To_Container()
        {
            // arrange, act
            _facade.AddCurrentAssembly();

            // assert
            var services = _services.BuildServiceProvider();
            Assert.IsType<TestModelRepository>(services.GetService<IEntityRepository<TestModel>>());
        }

        public class TestModel : Identifiable { }

        public class TestModelService : EntityResourceService<TestModel>
        {
            private static IEntityRepository<TestModel> _repo = new Mock<IEntityRepository<TestModel>>().Object;
            private static IJsonApiContext _jsonApiContext = new  Mock<IJsonApiContext>().Object;
            public TestModelService() : base(_jsonApiContext, _repo) { }
        }

        public class TestModelRepository : DefaultEntityRepository<TestModel>
        {
            internal static IDbContextResolver _dbContextResolver;
            private static IJsonApiContext _jsonApiContext = new  Mock<IJsonApiContext>().Object;
            public TestModelRepository() : base(_jsonApiContext, _dbContextResolver) { }
        }
    }
}
