using System.Collections.Generic;
using System.Linq;
using JsonApiDotNetCore;
using JsonApiDotNetCore.Hooks.Internal.Execution;
using JsonApiDotNetCoreExample.Data;
using JsonApiDotNetCoreExample.Models;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace UnitTests.ResourceHooks.Executor.Delete
{
    public sealed class BeforeDeleteWithDbValuesTests : HooksTestsSetup
    {
        private readonly ResourceHook[] _targetHooks = { ResourceHook.BeforeDelete, ResourceHook.BeforeImplicitUpdateRelationship, ResourceHook.BeforeUpdateRelationship };

        private readonly DbContextOptions<AppDbContext> _options;
        private readonly Person _person;
        public BeforeDeleteWithDbValuesTests()
        {
            _person = PersonFaker.Generate();
            var todo1 = TodoFaker.Generate();
            var todo2 = TodoFaker.Generate();
            var passport = PassportFaker.Generate();

            _person.Passport = passport;
            _person.TodoItems = new HashSet<TodoItem> { todo1 };
            _person.StakeHolderTodoItem = todo2;
            _options = InitInMemoryDb(context =>
            {
                context.Set<Person>().Add(_person);
                context.SaveChanges();
            });
        }

        [Fact]
        public void BeforeDelete()
        {
            // Arrange
            var personDiscovery = SetDiscoverableHooks<Person>(_targetHooks, EnableDbValues);
            var todoDiscovery = SetDiscoverableHooks<TodoItem>(_targetHooks, EnableDbValues);
            var passportDiscovery = SetDiscoverableHooks<Passport>(_targetHooks, EnableDbValues);
            var (_, hookExecutor, personResourceMock, todoResourceMock, passportResourceMock) = CreateTestObjects(personDiscovery, todoDiscovery, passportDiscovery, repoDbContextOptions: _options);

            // Act
            hookExecutor.BeforeDelete(_person.AsList(), ResourcePipeline.Delete);

            // Assert
            personResourceMock.Verify(rd => rd.BeforeDelete(It.IsAny<IResourceHashSet<Person>>(), It.IsAny<ResourcePipeline>()), Times.Once());
            todoResourceMock.Verify(rd => rd.BeforeImplicitUpdateRelationship(It.Is<IRelationshipsDictionary<TodoItem>>(rh => CheckImplicitTodoItems(rh)), ResourcePipeline.Delete), Times.Once());
            passportResourceMock.Verify(rd => rd.BeforeImplicitUpdateRelationship(It.Is<IRelationshipsDictionary<Passport>>(rh => CheckImplicitPassports(rh)), ResourcePipeline.Delete), Times.Once());
            VerifyNoOtherCalls(personResourceMock, todoResourceMock, passportResourceMock);
        }

        [Fact]
        public void BeforeDelete_No_Parent_Hooks()
        {
            // Arrange
            var personDiscovery = SetDiscoverableHooks<Person>(NoHooks, DisableDbValues);
            var todoDiscovery = SetDiscoverableHooks<TodoItem>(_targetHooks, EnableDbValues);
            var passportDiscovery = SetDiscoverableHooks<Passport>(_targetHooks, EnableDbValues);
            var (_, hookExecutor, personResourceMock, todoResourceMock, passportResourceMock) = CreateTestObjects(personDiscovery, todoDiscovery, passportDiscovery, repoDbContextOptions: _options);

            // Act
            hookExecutor.BeforeDelete(_person.AsList(), ResourcePipeline.Delete);

            // Assert
            todoResourceMock.Verify(rd => rd.BeforeImplicitUpdateRelationship(It.Is<IRelationshipsDictionary<TodoItem>>(rh => CheckImplicitTodoItems(rh)), ResourcePipeline.Delete), Times.Once());
            passportResourceMock.Verify(rd => rd.BeforeImplicitUpdateRelationship(It.Is<IRelationshipsDictionary<Passport>>(rh => CheckImplicitPassports(rh)), ResourcePipeline.Delete), Times.Once());
            VerifyNoOtherCalls(personResourceMock, todoResourceMock, passportResourceMock);
        }

        [Fact]
        public void BeforeDelete_No_Children_Hooks()
        {
            // Arrange
            var personDiscovery = SetDiscoverableHooks<Person>(_targetHooks, EnableDbValues);
            var todoDiscovery = SetDiscoverableHooks<TodoItem>(NoHooks, DisableDbValues);
            var passportDiscovery = SetDiscoverableHooks<Passport>(NoHooks, DisableDbValues);
            var (_, hookExecutor, personResourceMock, todoResourceMock, passportResourceMock) = CreateTestObjects(personDiscovery, todoDiscovery, passportDiscovery, repoDbContextOptions: _options);

            // Act
            hookExecutor.BeforeDelete(_person.AsList(), ResourcePipeline.Delete);

            // Assert
            personResourceMock.Verify(rd => rd.BeforeDelete(It.IsAny<IResourceHashSet<Person>>(), It.IsAny<ResourcePipeline>()), Times.Once());
            VerifyNoOtherCalls(personResourceMock, todoResourceMock, passportResourceMock);
        }

        private bool CheckImplicitTodoItems(IRelationshipsDictionary<TodoItem> rh)
        {
            var todoItems = rh.GetByRelationship<Person>();
            return todoItems.Count == 2;
        }

        private bool CheckImplicitPassports(IRelationshipsDictionary<Passport> rh)
        {
            var passports = rh.GetByRelationship<Person>().Single().Value;
            return passports.Count == 1;
        }
    }
}

