using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JsonApiDotNetCore.Internal;
using JsonApiDotNetCore.Internal.Generics;
using JsonApiDotNetCore.Models;
using JsonApiDotNetCore.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace JsonApiDotNetCore.Serialization
{
    public class JsonApiDeSerializer : IJsonApiDeSerializer
    {
        private readonly IJsonApiContext _jsonApiContext;
        private readonly IGenericProcessorFactory _genericProcessorFactory;

        public JsonApiDeSerializer(
            IJsonApiContext jsonApiContext,
            IGenericProcessorFactory genericProcessorFactory)
        {
            _jsonApiContext = jsonApiContext;
            _genericProcessorFactory = genericProcessorFactory;
        }

        public object Deserialize(string requestBody)
        {
            try
            {
                var document = JsonConvert.DeserializeObject<Document>(requestBody);
                var entity = DocumentToObject(document.Data);
                return entity;
            }
            catch (Exception e)
            {
                throw new JsonApiException(400, "Failed to deserialize request body", e);
            }
        }

        public TEntity Deserialize<TEntity>(string requestBody) => (TEntity)Deserialize(requestBody);

        public object DeserializeRelationship(string requestBody)
        {
            try
            {
                var data = JToken.Parse(requestBody)["data"];

                if (data is JArray)
                    return data.ToObject<List<DocumentData>>();

                return new List<DocumentData> { data.ToObject<DocumentData>() };
            }
            catch (Exception e)
            {
                throw new JsonApiException(400, "Failed to deserialize request body", e);
            }
        }

        public List<TEntity> DeserializeList<TEntity>(string requestBody)
        {
            try
            {
                var documents = JsonConvert.DeserializeObject<Documents>(requestBody);

                var deserializedList = new List<TEntity>();
                foreach (var data in documents.Data)
                {
                    var entity = DocumentToObject(data);
                    deserializedList.Add((TEntity)entity);
                }

                return deserializedList;
            }
            catch (Exception e)
            {
                throw new JsonApiException(400, "Failed to deserialize request body", e);
            }
        }

        private object DocumentToObject(DocumentData data)
        {
            var contextEntity = _jsonApiContext.ContextGraph.GetContextEntity(data.Type);
            _jsonApiContext.RequestEntity = contextEntity;

            var entity = Activator.CreateInstance(contextEntity.EntityType);

            entity = SetEntityAttributes(entity, contextEntity, data.Attributes);
            entity = SetRelationships(entity, contextEntity, data.Relationships);

            var identifiableEntity = (IIdentifiable)entity;

            if (data.Id != null)
                identifiableEntity.StringId = data.Id;

            return identifiableEntity;
        }

        private object SetEntityAttributes(
            object entity, ContextEntity contextEntity, Dictionary<string, object> attributeValues)
        {
            if (attributeValues == null || attributeValues.Count == 0)
                return entity;

            var entityProperties = entity.GetType().GetProperties();

            foreach (var attr in contextEntity.Attributes)
            {
                var entityProperty = entityProperties.FirstOrDefault(p => p.Name == attr.InternalAttributeName);

                if (entityProperty == null)
                    throw new ArgumentException($"{contextEntity.EntityType.Name} does not contain an attribute named {attr.InternalAttributeName}", nameof(entity));

                if (attributeValues.TryGetValue(attr.PublicAttributeName, out object newValue))
                {
                    var convertedValue = ConvertAttrValue(newValue, entityProperty.PropertyType);
                    entityProperty.SetValue(entity, convertedValue);

                    if (attr.IsImmutable == false)
                        _jsonApiContext.AttributesToUpdate[attr] = convertedValue;
                }
            }

            return entity;
        }

        private object ConvertAttrValue(object newValue, Type targetType)
        {
            if (newValue is JContainer jObject)
                return DeserializeComplexType(jObject, targetType);

            var convertedValue = TypeHelper.ConvertType(newValue, targetType);
            return convertedValue;
        }

        private object DeserializeComplexType(JContainer obj, Type targetType)
        {
            return obj.ToObject(targetType, JsonSerializer.Create(_jsonApiContext.Options.SerializerSettings));
        }

        private object SetRelationships(
            object entity,
            ContextEntity contextEntity,
            Dictionary<string, RelationshipData> relationships)
        {
            if (relationships == null || relationships.Count == 0)
                return entity;

            var entityProperties = entity.GetType().GetProperties();

            foreach (var attr in contextEntity.Relationships)
            {
                entity = attr.IsHasOne
                    ? SetHasOneRelationship(entity, entityProperties, attr, contextEntity, relationships)
                    : SetHasManyRelationship(entity, entityProperties, attr, contextEntity, relationships);
            }

            return entity;
        }

        private object SetHasOneRelationship(object entity,
            PropertyInfo[] entityProperties,
            RelationshipAttribute attr,
            ContextEntity contextEntity,
            Dictionary<string, RelationshipData> relationships)
        {
            var entityProperty = entityProperties.FirstOrDefault(p => p.Name == $"{attr.InternalRelationshipName}Id");

            if (entityProperty == null)
                throw new JsonApiException(400, $"{contextEntity.EntityType.Name} does not contain an relationsip named {attr.InternalRelationshipName}");

            var relationshipName = attr.PublicRelationshipName;

            if (relationships.TryGetValue(relationshipName, out RelationshipData relationshipData))
            {
                var relationshipAttr = _jsonApiContext.RequestEntity.Relationships
                        .SingleOrDefault(r => r.PublicRelationshipName == relationshipName);

                var data = (Dictionary<string, object>)relationshipData.ExposedData;

                if (data == null) return entity;

                var newValue = data["id"];
                var convertedValue = TypeHelper.ConvertType(newValue, entityProperty.PropertyType);

                _jsonApiContext.RelationshipsToUpdate[relationshipAttr] = convertedValue;

                entityProperty.SetValue(entity, convertedValue);
            }

            return entity;
        }

        private object SetHasManyRelationship(object entity,
            PropertyInfo[] entityProperties,
            RelationshipAttribute attr,
            ContextEntity contextEntity,
            Dictionary<string, RelationshipData> relationships)
        {
            var entityProperty = entityProperties.FirstOrDefault(p => p.Name == attr.InternalRelationshipName);

            if (entityProperty == null)
                throw new JsonApiException(400, $"{contextEntity.EntityType.Name} does not contain an relationsip named {attr.InternalRelationshipName}");

            var relationshipName = attr.PublicRelationshipName;

            if (relationships.TryGetValue(relationshipName, out RelationshipData relationshipData))
            {
                var data = (List<Dictionary<string, object>>)relationshipData.ExposedData;

                if (data == null) return entity;

                var genericProcessor = _genericProcessorFactory.GetProcessor(attr.Type);
                var ids = relationshipData.ManyData.Select(r => r["id"].ToString());
                genericProcessor.SetRelationships(entity, attr, ids);
            }

            return entity;
        }
    }
}