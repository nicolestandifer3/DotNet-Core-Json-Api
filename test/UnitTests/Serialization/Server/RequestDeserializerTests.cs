using System.Collections.Generic;
using System.ComponentModel.Design;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Resources.Annotations;
using JsonApiDotNetCore.Serialization;
using JsonApiDotNetCore.Serialization.Objects;
using Moq;
using Newtonsoft.Json;
using Xunit;

namespace UnitTests.Serialization.Server
{
    public sealed class RequestDeserializerTests : DeserializerTestsSetup
    {
        private readonly RequestDeserializer _deserializer;
        private readonly Mock<ITargetedFields> _fieldsManagerMock = new Mock<ITargetedFields>();
        private readonly Mock<IJsonApiRequest> _requestMock = new Mock<IJsonApiRequest>();

        public RequestDeserializerTests()
        {
            _deserializer = new RequestDeserializer(ResourceGraph, new ResourceFactory(new ServiceContainer()), _fieldsManagerMock.Object,
                MockHttpContextAccessor.Object, _requestMock.Object, new JsonApiOptions());
        }

        [Fact]
        public void DeserializeAttributes_VariousUpdatedMembers_RegistersTargetedFields()
        {
            // Arrange
            SetupFieldsManager(out HashSet<AttrAttribute> attributesToUpdate, out HashSet<RelationshipAttribute> relationshipsToUpdate);
            Document content = CreateTestResourceDocument();
            string body = JsonConvert.SerializeObject(content);

            // Act
            _deserializer.Deserialize(body);

            // Assert
            Assert.Equal(5, attributesToUpdate.Count);
            Assert.Empty(relationshipsToUpdate);
        }

        [Fact]
        public void DeserializeRelationships_MultipleDependentRelationships_RegistersUpdatedRelationships()
        {
            // Arrange
            SetupFieldsManager(out HashSet<AttrAttribute> attributesToUpdate, out HashSet<RelationshipAttribute> relationshipsToUpdate);
            Document content = CreateDocumentWithRelationships("multiPrincipals");
            content.SingleData.Relationships.Add("populatedToOne", CreateRelationshipData("oneToOneDependents"));
            content.SingleData.Relationships.Add("emptyToOne", CreateRelationshipData());
            content.SingleData.Relationships.Add("populatedToManies", CreateRelationshipData("oneToManyDependents", true));
            content.SingleData.Relationships.Add("emptyToManies", CreateRelationshipData(isToManyData: true));
            string body = JsonConvert.SerializeObject(content);

            // Act
            _deserializer.Deserialize(body);

            // Assert
            Assert.Equal(4, relationshipsToUpdate.Count);
            Assert.Empty(attributesToUpdate);
        }

        [Fact]
        public void DeserializeRelationships_MultiplePrincipalRelationships_RegistersUpdatedRelationships()
        {
            // Arrange
            SetupFieldsManager(out HashSet<AttrAttribute> attributesToUpdate, out HashSet<RelationshipAttribute> relationshipsToUpdate);
            Document content = CreateDocumentWithRelationships("multiDependents");
            content.SingleData.Relationships.Add("populatedToOne", CreateRelationshipData("oneToOnePrincipals"));
            content.SingleData.Relationships.Add("emptyToOne", CreateRelationshipData());
            content.SingleData.Relationships.Add("populatedToMany", CreateRelationshipData("oneToManyPrincipals"));
            content.SingleData.Relationships.Add("emptyToMany", CreateRelationshipData());
            string body = JsonConvert.SerializeObject(content);

            // Act
            _deserializer.Deserialize(body);

            // Assert
            Assert.Equal(4, relationshipsToUpdate.Count);
            Assert.Empty(attributesToUpdate);
        }

        private void SetupFieldsManager(out HashSet<AttrAttribute> attributesToUpdate, out HashSet<RelationshipAttribute> relationshipsToUpdate)
        {
            attributesToUpdate = new HashSet<AttrAttribute>();
            relationshipsToUpdate = new HashSet<RelationshipAttribute>();
            _fieldsManagerMock.Setup(m => m.Attributes).Returns(attributesToUpdate);
            _fieldsManagerMock.Setup(m => m.Relationships).Returns(relationshipsToUpdate);
        }
    }
}
