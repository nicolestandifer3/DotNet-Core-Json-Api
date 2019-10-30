using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JsonApiDotNetCore.Models;
using JsonApiDotNetCore.Serialization;
using Moq;
using Xunit;
using UnitTests.TestModels;
using Person = UnitTests.TestModels.Person;
namespace UnitTests.Serialization.Serializer
{
    public class BaseDocumentBuilderTests : SerializerTestsSetup
    {
        private readonly TestDocumentBuilder _builder;

        public BaseDocumentBuilderTests()
        {
            var mock = new Mock<IResourceObjectBuilder>();
            mock.Setup(m => m.Build(It.IsAny<IIdentifiable>(), It.IsAny<IEnumerable<AttrAttribute>>(), It.IsAny<IEnumerable<RelationshipAttribute>>())).Returns(new ResourceObject());
            _builder = new TestDocumentBuilder(mock.Object, _resourceGraph);
        }


        [Fact]
        public void EntityToDocument_NullEntity_CanBuild()
        {
            // Arrange
            TestResource entity = null;

            // Act
            var document = _builder.Build(entity, null, null);

            // Assert
            Assert.Null(document.Data);
            Assert.False(document.IsPopulated);
        }


        [Fact]
        public void EntityToDocument_EmptyList_CanBuild()
        {
            // Arrange
            var entities = new List<TestResource>();

            // Act
            var document = _builder.Build(entities, null, null);

            // Assert
            Assert.NotNull(document.Data);
            Assert.Empty(document.ManyData);
        }


        [Fact]
        public void EntityToDocument_SingleEntity_CanBuild()
        {
            // Arrange
            IIdentifiable dummy = new Identifiable();

            // Act
            var document = _builder.Build(dummy, null, null);

            // Assert
            Assert.NotNull(document.Data);
            Assert.True(document.IsPopulated);
        }

        [Fact]
        public void EntityToDocument_EntityList_CanBuild()
        {
            // Arrange
            var entities = new List<IIdentifiable>() { new Identifiable(), new Identifiable() };

            // Act
            var document = _builder.Build(entities, null, null);
            var data = (List<ResourceObject>)document.Data;

            // Assert
            Assert.Equal(2, data.Count);
        }
    }
}
