using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Humanizer;
using JsonApiDotNetCore.Builders;
using JsonApiDotNetCore.Extensions;
using JsonApiDotNetCore.Graph;
using JsonApiDotNetCore.Internal.Contracts;
using JsonApiDotNetCore.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace UnitTests
{
    public class ResourceGraphBuilder_Tests
    {
        class NonDbResource : Identifiable { }
        class DbResource : Identifiable { }
        class TestContext : DbContext
        {
            public DbSet<DbResource> DbResources { get; set; }
        }

        [Fact]
        public void Can_Build_ResourceGraph_Using_Builder()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddJsonApi<TestContext>(resources: builder => builder.AddResource<NonDbResource>("non-db-resources"));

            // Act
            var container = services.BuildServiceProvider();

            // Assert
            var resourceGraph = container.GetRequiredService<IResourceGraph>();
            var dbResource = resourceGraph.GetResourceContext("db-resources");
            var nonDbResource = resourceGraph.GetResourceContext("non-db-resources");
            Assert.Equal(typeof(DbResource), dbResource.ResourceType);
            Assert.Equal(typeof(NonDbResource), nonDbResource.ResourceType);
            Assert.Equal(typeof(ResourceDefinition<NonDbResource>), nonDbResource.ResourceDefinitionType);
        }

        [Fact]
        public void Resources_Without_Names_Specified_Will_Use_Default_Formatter()
        {
            // Arrange
            var builder = new ResourceGraphBuilder();
            builder.AddResource<TestResource>();

            // Act
            var resourceGraph = builder.Build();

            // Assert
            var resource = resourceGraph.GetResourceContext(typeof(TestResource));
            Assert.Equal("test-resources", resource.ResourceName);
        }

        [Fact]
        public void Resources_Without_Names_Specified_Will_Use_Configured_Formatter()
        {
            // Arrange
            var builder = new ResourceGraphBuilder(new CamelCaseNameFormatter());
            builder.AddResource<TestResource>();

            // Act
            var resourceGraph = builder.Build();

            // Assert
            var resource = resourceGraph.GetResourceContext(typeof(TestResource));
            Assert.Equal("testResources", resource.ResourceName);
        }

        [Fact]
        public void Attrs_Without_Names_Specified_Will_Use_Default_Formatter()
        {
            // Arrange
            var builder = new ResourceGraphBuilder();
            builder.AddResource<TestResource>();

            // Act
            var resourceGraph = builder.Build();

            // Assert
            var resource = resourceGraph.GetResourceContext(typeof(TestResource));
            Assert.Contains(resource.Attributes, (i) => i.PublicAttributeName == "compound-attribute");
        }

        [Fact]
        public void Attrs_Without_Names_Specified_Will_Use_Configured_Formatter()
        {
            // Arrange
            var builder = new ResourceGraphBuilder(new CamelCaseNameFormatter());
            builder.AddResource<TestResource>();

            // Act
            var resourceGraph = builder.Build();

            // Assert
            var resource = resourceGraph.GetResourceContext(typeof(TestResource));
            Assert.Contains(resource.Attributes, (i) => i.PublicAttributeName == "compoundAttribute");
        }

        [Fact]
        public void Relationships_Without_Names_Specified_Will_Use_Default_Formatter()
        {
            // Arrange
            var builder = new ResourceGraphBuilder();
            builder.AddResource<TestResource>();

            // Act
            var resourceGraph = builder.Build();

            // Assert
            var resource = resourceGraph.GetResourceContext(typeof(TestResource));
            Assert.Equal("related-resource", resource.Relationships.Single(r => r.IsHasOne).PublicRelationshipName);
            Assert.Equal("related-resources", resource.Relationships.Single(r => r.IsHasMany).PublicRelationshipName);
        }

        public class TestResource : Identifiable
        {
            [Attr] public string CompoundAttribute { get; set; }
            [HasOne] public RelatedResource RelatedResource { get; set; }
            [HasMany] public List<RelatedResource> RelatedResources { get; set; }
        }

        public class RelatedResource : Identifiable { }

        public class CamelCaseNameFormatter : IResourceNameFormatter
        {
            public string ApplyCasingConvention(string properName) => ToCamelCase(properName);

            public string FormatPropertyName(PropertyInfo property) => ToCamelCase(property.Name);

            public string FormatResourceName(Type resourceType) => ToCamelCase(resourceType.Name.Pluralize());

            private string ToCamelCase(string str) => Char.ToLowerInvariant(str[0]) + str.Substring(1);
        }
    }
}
