
using Bogus;
using JsonApiDotNetCore.Builders;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Internal;
using JsonApiDotNetCore.Internal.Generics;
using JsonApiDotNetCore.Models;
using JsonApiDotNetCore.Services;
using JsonApiDotNetCoreExample.Data;
using JsonApiDotNetCoreExample.Models;
using JsonApiDotNetCoreExample.Resources;
using Microsoft.EntityFrameworkCore;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Person = JsonApiDotNetCoreExample.Models.Person;

namespace UnitTests.ResourceHooks
{

    public class ResourceHooksTestBase
    {

        protected ResourceHook[] AllHooks;
        protected ResourceHook[] NoHooks = new ResourceHook[0];
        protected ResourceHook[] AllHooksNoImplicit;
        protected ResourceHook[] EnableDbValuesEverywhere = { ResourceHook.BeforeUpdate, ResourceHook.BeforeUpdateRelationship };
        protected readonly Faker<Person> _personFaker;
        protected readonly Faker<TodoItem> _todoFaker;
        protected readonly Faker<Tag> _tagFaker;
        protected readonly Faker<Article> _articleFaker;
        protected readonly Faker<ArticleTag> _articleTagFaker;
        protected readonly Faker<IdentifiableArticleTag> _identifiableArticleTagFaker;
        protected readonly Faker<Passport> _passportFaker;
        public ResourceHooksTestBase()
        {
            AllHooks = Enum.GetValues(typeof(ResourceHook))
                .Cast<ResourceHook>()
                .Where(h => h != ResourceHook.None)
                .ToArray();

            AllHooksNoImplicit = AllHooks.Where(h => h != ResourceHook.BeforeImplicitUpdateRelationship).ToArray();

            _todoFaker = new Faker<TodoItem>().Rules((f, i) => i.Id = f.UniqueIndex + 1);
            _personFaker = new Faker<Person>().Rules((f, i) => i.Id = f.UniqueIndex + 1);

            _articleFaker = new Faker<Article>().Rules((f, i) => i.Id = f.UniqueIndex + 1);
            _articleTagFaker = new Faker<ArticleTag>();
            _identifiableArticleTagFaker = new Faker<IdentifiableArticleTag>().Rules((f, i) => i.Id = f.UniqueIndex + 1);
            _tagFaker = new Faker<Tag>().Rules((f, i) => i.Id = f.UniqueIndex + 1);

            _passportFaker = new Faker<Passport>().Rules((f, i) => i.Id = f.UniqueIndex + 1);
        }

        protected List<TodoItem> CreateTodoWithToOnePerson()
        {
            var todoItem = _todoFaker.Generate();
            var person = _personFaker.Generate();
            var todoList = new List<TodoItem>() { todoItem };
            person.ToOneTodoItem = todoItem;
            todoItem.ToOnePerson = person;
            return todoList;
        }

        protected List<TodoItem> CreateTodoWithOwner()
        {
            var todoItem = _todoFaker.Generate();
            var person = _personFaker.Generate();
            var todoList = new List<TodoItem>() { todoItem };
            person.AssignedTodoItems = todoList;
            todoItem.Owner = person;
            return todoList;
        }
        protected (Mock<IJsonApiContext>, IResourceHookExecutor, Mock<IResourceHookContainer<TMain>>)
        CreateTestObjects<TMain>(IHooksDiscovery<TMain> discovery = null)
            where TMain : class, IIdentifiable<int>

        {
            // creates the resource definition mock and corresponding ImplementedHooks discovery instance
            var mainResource = CreateResourceDefinition(discovery);

            // mocking the GenericProcessorFactory and JsonApiContext and wiring them up.
            (var context, var processorFactory) = CreateContextAndProcessorMocks();

            // wiring up the mocked GenericProcessorFactory to return the correct resource definition
            SetupProcessorFactoryForResourceDefinition(processorFactory, mainResource.Object, discovery, context.Object);
            var meta = new HookExecutorHelper(context.Object.GenericProcessorFactory, ResourceGraph.Instance);
            var hookExecutor = new ResourceHookExecutor(meta, context.Object, ResourceGraph.Instance);

            return (context, hookExecutor, mainResource);
        }

        protected (Mock<IJsonApiContext> context, IResourceHookExecutor, Mock<IResourceHookContainer<TMain>>, Mock<IResourceHookContainer<TNested>>)
            CreateTestObjects<TMain, TNested>(
            IHooksDiscovery<TMain> mainDiscovery = null,
            IHooksDiscovery<TNested> nestedDiscovery = null,
            DbContextOptions<AppDbContext> repoDbContextOptions = null
            )
            where TMain : class, IIdentifiable<int>
            where TNested : class, IIdentifiable<int>
        {
            // creates the resource definition mock and corresponding for a given set of discoverable hooks
            var mainResource = CreateResourceDefinition(mainDiscovery);
            var nestedResource = CreateResourceDefinition(nestedDiscovery);

            // mocking the GenericProcessorFactory and JsonApiContext and wiring them up.
            (var context, var processorFactory) = CreateContextAndProcessorMocks();


            var dbContext = repoDbContextOptions != null ? new AppDbContext(repoDbContextOptions) : null;

            SetupProcessorFactoryForResourceDefinition(processorFactory, mainResource.Object, mainDiscovery, context.Object, dbContext);
            var meta = new HookExecutorHelper(context.Object.GenericProcessorFactory, ResourceGraph.Instance, context.Object);
            var hookExecutor = new ResourceHookExecutor(meta, context.Object, ResourceGraph.Instance);

            SetupProcessorFactoryForResourceDefinition(processorFactory, nestedResource.Object, nestedDiscovery, context.Object, dbContext);

            return (context, hookExecutor, mainResource, nestedResource);
        }

        protected (Mock<IJsonApiContext> context, IResourceHookExecutor, Mock<IResourceHookContainer<TMain>>, Mock<IResourceHookContainer<TFirstNested>>, Mock<IResourceHookContainer<TSecondNested>>)
            CreateTestObjects<TMain, TFirstNested, TSecondNested>(
            IHooksDiscovery<TMain> mainDiscovery = null,
            IHooksDiscovery<TFirstNested> firstNestedDiscovery = null,
            IHooksDiscovery<TSecondNested> secondNestedDiscovery = null,
            DbContextOptions<AppDbContext> repoDbContextOptions = null
            )
            where TMain : class, IIdentifiable<int>
            where TFirstNested : class, IIdentifiable<int>
            where TSecondNested : class, IIdentifiable<int>
        {
            // creates the resource definition mock and corresponding for a given set of discoverable hooks
            var mainResource = CreateResourceDefinition(mainDiscovery);
            var firstNestedResource = CreateResourceDefinition(firstNestedDiscovery);
            var secondNestedResource = CreateResourceDefinition(secondNestedDiscovery);

            // mocking the GenericProcessorFactory and JsonApiContext and wiring them up.
            (var context, var processorFactory) = CreateContextAndProcessorMocks();

            var dbContext = repoDbContextOptions != null ? new AppDbContext(repoDbContextOptions) : null;

            SetupProcessorFactoryForResourceDefinition(processorFactory, mainResource.Object, mainDiscovery, context.Object, dbContext);
            var meta = new HookExecutorHelper(context.Object.GenericProcessorFactory, ResourceGraph.Instance);
            var hookExecutor = new ResourceHookExecutor(meta, context.Object, ResourceGraph.Instance);

            SetupProcessorFactoryForResourceDefinition(processorFactory, firstNestedResource.Object, firstNestedDiscovery, context.Object, dbContext);
            SetupProcessorFactoryForResourceDefinition(processorFactory, secondNestedResource.Object, secondNestedDiscovery, context.Object, dbContext);

            return (context, hookExecutor, mainResource, firstNestedResource, secondNestedResource);
        }

        protected IHooksDiscovery<TEntity> SetDiscoverableHooks<TEntity>(ResourceHook[] implementedHooks = null, params ResourceHook[] enableDbValuesHooks)
            where TEntity : class, IIdentifiable<int>
        {
            implementedHooks = implementedHooks ?? AllHooks;
            var mock = new Mock<IHooksDiscovery<TEntity>>();
            mock.Setup(discovery => discovery.ImplementedHooks)
                .Returns(implementedHooks);

            if (!enableDbValuesHooks.Any())
            {
                mock.Setup(discovery => discovery.DatabaseDiffDisabledHooks)
                .Returns(AllHooksNoImplicit);
            }
            mock.Setup(discovery => discovery.DatabaseDiffEnabledHooks)
                .Returns( new ResourceHook[] { ResourceHook.BeforeImplicitUpdateRelationship }.Concat(enableDbValuesHooks).ToArray());

            return mock.Object;
        }

        private Mock<IResourceHookContainer<TModel>> CreateResourceDefinition
            <TModel>(IHooksDiscovery<TModel> discovery
            )
            where TModel : class, IIdentifiable<int>
        {
            var resourceDefinition = new Mock<IResourceHookContainer<TModel>>();
            MockHooks(resourceDefinition, discovery);
            return resourceDefinition;
        }

        private void MockHooks<TModel>(
            Mock<IResourceHookContainer<TModel>> resourceDefinition,
            IHooksDiscovery<TModel> discovery
            ) where TModel : class, IIdentifiable<int>
        {
            resourceDefinition
               .Setup(rd => rd.BeforeCreate(It.IsAny<IEnumerable<TModel>>(), It.IsAny<ResourceAction>()))
               .Returns<IEnumerable<TModel>, ResourceAction>((entities, context) => entities)
               .Verifiable();
            resourceDefinition
                .Setup(rd => rd.AfterCreate(It.IsAny<IEnumerable<TModel>>(), It.IsAny<HookExecutionContext<TModel>>()))
                .Returns<IEnumerable<TModel>, HookExecutionContext<TModel>>((entities, context) => entities)
                .Verifiable();
            resourceDefinition
                .Setup(rd => rd.BeforeRead(It.IsAny<ResourceAction>(), It.IsAny<bool>(), It.IsAny<string>()))
                .Verifiable();
            resourceDefinition
                .Setup(rd => rd.AfterRead(It.IsAny<IEnumerable<TModel>>(), It.IsAny<ResourceAction>(), It.IsAny<bool>()))
                .Returns<IEnumerable<TModel>, ResourceAction, bool>((entities, context, nested) => entities)
                .Verifiable();
            resourceDefinition
                .Setup(rd => rd.BeforeUpdate(It.IsAny<EntityDiff<TModel>>(), It.IsAny<ResourceAction>()))
                .Returns<EntityDiff<TModel>, ResourceAction>((entityDiff, context) => entityDiff.RequestEntities)
                .Verifiable();
            resourceDefinition
                .Setup(rd => rd.BeforeUpdateRelationship(It.IsAny<IEnumerable<string>>(), It.IsAny<IUpdatedRelationshipHelper<TModel>>(), It.IsAny<ResourceAction>()))
                .Returns<IEnumerable<string>, IUpdatedRelationshipHelper<TModel>, ResourceAction>((ids, context, helper) => ids)
                .Verifiable();
            resourceDefinition
                .Setup(rd => rd.AfterUpdate(It.IsAny<IEnumerable<TModel>>(), It.IsAny<HookExecutionContext<TModel>>()))
                .Returns<IEnumerable<TModel>, HookExecutionContext<TModel>>((entities, context) => entities)
                .Verifiable();
            resourceDefinition
                .Setup(rd => rd.BeforeDelete(It.IsAny<IEnumerable<TModel>>(), It.IsAny<ResourceAction>()))
                .Returns<IEnumerable<TModel>, ResourceAction>((entities, context) => entities)
                .Verifiable();
            resourceDefinition
                .Setup(rd => rd.AfterDelete(It.IsAny<IEnumerable<TModel>>(), It.IsAny<HookExecutionContext<TModel>>(), It.IsAny<bool>()))
                .Verifiable();
            resourceDefinition
                .Setup(rd => rd.BeforeImplicitUpdateRelationship(It.IsAny<IUpdatedRelationshipHelper<TModel>>(), It.IsAny<ResourceAction>()))
                .Verifiable();
        }


        private (Mock<IJsonApiContext>, Mock<IGenericProcessorFactory>) CreateContextAndProcessorMocks()
        {
            var processorFactory = new Mock<IGenericProcessorFactory>();
            var context = new Mock<IJsonApiContext>();
            context.Setup(c => c.GenericProcessorFactory).Returns(processorFactory.Object);
            context.Setup(c => c.Options).Returns(new JsonApiOptions { DatabaseValuesInDiffs = false });
            context.Setup(c => c.ResourceGraph).Returns(ResourceGraph.Instance);

            return (context, processorFactory);
        }

        private void SetupProcessorFactoryForResourceDefinition<TModel>(
            Mock<IGenericProcessorFactory> processorFactory,
            IResourceHookContainer<TModel> modelResource,
            IHooksDiscovery<TModel> discovery,
            IJsonApiContext apiContext,
            AppDbContext dbContext = null
            )
            where TModel : class, IIdentifiable<int>
        {
            processorFactory.Setup(c => c.GetProcessor<IResourceHookContainer>(typeof(ResourceDefinition<>), typeof(TModel)))
            .Returns(modelResource);

            processorFactory.Setup(c => c.GetProcessor<IHooksDiscovery>(typeof(IHooksDiscovery<>), typeof(TModel)))
            .Returns(discovery);

            if (dbContext != null)
            {
                var repo = CreateTestRepository<TModel>(dbContext, apiContext);
                processorFactory.Setup(c => c.GetProcessor<IEntityReadRepository<TModel, int>>(typeof(IEntityRepository<>), typeof(TModel))).Returns(repo);
            }
        }

        protected DbContextOptions<AppDbContext> InitInMemoryDb(Action<DbContext> seeder)
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: "repository_mock")
            .Options;

            using (var context = new AppDbContext(options))
            {
                seeder(context);
            }
            return options;
        }


        protected IEntityRepository<TModel, int> CreateTestRepository<TModel>(
            AppDbContext dbContext,
            IJsonApiContext apiContext
            ) where TModel : class, IIdentifiable<int>
        {
            IDbContextResolver resolver = CreateTestDbResolver<TModel>(dbContext);
            return new DefaultEntityRepository<TModel, int>(apiContext, resolver);
        }

        private IDbContextResolver CreateTestDbResolver<TModel>(AppDbContext dbContext) where TModel : class, IIdentifiable<int>
        {
            var mock = new Mock<IDbContextResolver>();
            mock.Setup(r => r.GetContext()).Returns(dbContext);
            mock.Setup(r => r.GetDbSet<TModel>()).Returns(dbContext.Set<TModel>());
            return mock.Object;
        }

        protected void VerifyNoOtherCalls(params dynamic[] resourceMocks)
        {
            foreach (var mock in resourceMocks)
            {
                mock.VerifyNoOtherCalls();
            }
        }

    }
}

