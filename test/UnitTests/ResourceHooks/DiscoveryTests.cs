using JsonApiDotNetCore.Models;
using JsonApiDotNetCore.Hooks;
using System.Collections.Generic;
using Xunit;
using JsonApiDotNetCore.Builders;
using JsonApiDotNetCore.Internal;
using JsonApiDotNetCore.Internal.Contracts;
using System;
using Microsoft.Extensions.DependencyInjection;

namespace UnitTests.ResourceHooks
{
    public class DiscoveryTests
    {
        public class Dummy : Identifiable { }
        public class DummyResourceDefinition : ResourceDefinition<Dummy>
        {
            public DummyResourceDefinition() : base(new ResourceGraphBuilder().AddResource<Dummy>().Build()) { }

            public override IEnumerable<Dummy> BeforeDelete(IEntityHashSet<Dummy> affected, ResourcePipeline pipeline) { return affected; }
            public override void AfterDelete(HashSet<Dummy> entities, ResourcePipeline pipeline, bool succeeded) { }
        }

        private IServiceProvider MockProvider<TResource>(object service) where TResource : class, IIdentifiable
        {
            var services = new ServiceCollection();
            services.AddScoped((_) => (ResourceDefinition<TResource>)service);
            return services.BuildServiceProvider();
        }

        [Fact]
        public void HookDiscovery_StandardResourceDefinition_CanDiscover()
        {
            // Arrange & act
            var hookConfig = new HooksDiscovery<Dummy>(MockProvider<Dummy>(new DummyResourceDefinition()));
            // Assert
            Assert.Contains(ResourceHook.BeforeDelete, hookConfig.ImplementedHooks);
            Assert.Contains(ResourceHook.AfterDelete, hookConfig.ImplementedHooks);
        }

        public class AnotherDummy : Identifiable { }
        public abstract class ResourceDefinitionBase<T> : ResourceDefinition<T> where T : class, IIdentifiable
        {
            public ResourceDefinitionBase(IResourceGraph resourceGraph) : base(resourceGraph) { }
            public override IEnumerable<T> BeforeDelete(IEntityHashSet<T> entities, ResourcePipeline pipeline) { return entities; }
            public override void AfterDelete(HashSet<T> entities, ResourcePipeline pipeline, bool succeeded) { }
        }

        public class AnotherDummyResourceDefinition : ResourceDefinitionBase<AnotherDummy>
        {
            public AnotherDummyResourceDefinition() : base(new ResourceGraphBuilder().AddResource<AnotherDummy>().Build()) { }
        }

        [Fact]
        public void HookDiscovery_InheritanceSubclass_CanDiscover()
        {
            // Arrange & act
            var hookConfig = new HooksDiscovery<AnotherDummy>(MockProvider<AnotherDummy>(new AnotherDummyResourceDefinition()));
            // Assert
            Assert.Contains(ResourceHook.BeforeDelete, hookConfig.ImplementedHooks);
            Assert.Contains(ResourceHook.AfterDelete, hookConfig.ImplementedHooks);
        }

        public class YetAnotherDummy : Identifiable { }
        public class YetAnotherDummyResourceDefinition : ResourceDefinition<YetAnotherDummy>
        {
            public YetAnotherDummyResourceDefinition() : base(new ResourceGraphBuilder().AddResource<YetAnotherDummy>().Build()) { }

            public override IEnumerable<YetAnotherDummy> BeforeDelete(IEntityHashSet<YetAnotherDummy> affected, ResourcePipeline pipeline) { return affected; }

            [LoadDatabaseValues(false)]
            public override void AfterDelete(HashSet<YetAnotherDummy> entities, ResourcePipeline pipeline, bool succeeded) { }
        }

        [Fact]
        public void HookDiscovery_WronglyUsedLoadDatabaseValueAttribute_ThrowsJsonApiSetupException()
        {
            //  assert
            Assert.Throws<JsonApiSetupException>(() =>
            {
                // Arrange & act
                var hookConfig = new HooksDiscovery<YetAnotherDummy>(MockProvider<YetAnotherDummy>(new YetAnotherDummyResourceDefinition()));
            });
        }

        [Fact]
        public void HookDiscovery_InheritanceWithGenericSubclass_CanDiscover()
        {
            // Arrange & act
            var hookConfig = new HooksDiscovery<AnotherDummy>(MockProvider<AnotherDummy>(new GenericDummyResourceDefinition<AnotherDummy>()));

            // Assert
            Assert.Contains(ResourceHook.BeforeDelete, hookConfig.ImplementedHooks);
            Assert.Contains(ResourceHook.AfterDelete, hookConfig.ImplementedHooks);
        }

        public class GenericDummyResourceDefinition<TResource> : ResourceDefinition<TResource> where TResource : class, IIdentifiable<int>
        {
            public GenericDummyResourceDefinition() : base(new ResourceGraphBuilder().AddResource<TResource>().Build()) { }

            public override IEnumerable<TResource> BeforeDelete(IEntityHashSet<TResource> entities, ResourcePipeline pipeline) { return entities; }
            public override void AfterDelete(HashSet<TResource> entities, ResourcePipeline pipeline, bool succeeded) { }
        }
    }
}
