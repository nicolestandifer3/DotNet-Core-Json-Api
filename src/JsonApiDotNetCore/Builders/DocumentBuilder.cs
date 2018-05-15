using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JsonApiDotNetCore.Extensions;
using JsonApiDotNetCore.Internal;
using JsonApiDotNetCore.Models;
using JsonApiDotNetCore.Services;

namespace JsonApiDotNetCore.Builders
{
    public class DocumentBuilder : IDocumentBuilder
    {
        private readonly IJsonApiContext _jsonApiContext;
        private readonly IContextGraph _contextGraph;
        private readonly IRequestMeta _requestMeta;
        private readonly DocumentBuilderOptions _documentBuilderOptions; 

        public DocumentBuilder(IJsonApiContext jsonApiContext, IRequestMeta requestMeta=null, IDocumentBuilderOptionsProvider documentBuilderOptionsProvider=null)
        {
            _jsonApiContext = jsonApiContext;
            _contextGraph = jsonApiContext.ContextGraph;
            _requestMeta = requestMeta;
            _documentBuilderOptions = documentBuilderOptionsProvider?.GetDocumentBuilderOptions() ?? new DocumentBuilderOptions(); ;
        }

        public Document Build(IIdentifiable entity)
        {
            var contextEntity = _contextGraph.GetContextEntity(entity.GetType());

            var document = new Document
            {
                Data = GetData(contextEntity, entity),
                Meta = GetMeta(entity)
            };

            if (ShouldIncludePageLinks(contextEntity))
                document.Links = _jsonApiContext.PageManager.GetPageLinks(new LinkBuilder(_jsonApiContext));

            document.Included = AppendIncludedObject(document.Included, contextEntity, entity);

            return document;
        }

        public Documents Build(IEnumerable<IIdentifiable> entities)
        {
            var entityType = entities.GetElementType();

            var contextEntity = _contextGraph.GetContextEntity(entityType);

            var enumeratedEntities = entities as IList<IIdentifiable> ?? entities.ToList();
            var documents = new Documents
            {
                Data = new List<DocumentData>(),
                Meta = GetMeta(enumeratedEntities.FirstOrDefault())
            };

            if (ShouldIncludePageLinks(contextEntity))
                documents.Links = _jsonApiContext.PageManager.GetPageLinks(new LinkBuilder(_jsonApiContext));

            foreach (var entity in enumeratedEntities)
            {
                documents.Data.Add(GetData(contextEntity, entity));
                documents.Included = AppendIncludedObject(documents.Included, contextEntity, entity);
            }

            return documents;
        }

        private Dictionary<string, object> GetMeta(IIdentifiable entity)
        {
            if (entity == null) return null;

            var builder = _jsonApiContext.MetaBuilder;

            if (entity is IHasMeta metaEntity)
                builder.Add(metaEntity.GetMeta(_jsonApiContext));

            if (_jsonApiContext.Options.IncludeTotalRecordCount)
                builder.Add("total-records", _jsonApiContext.PageManager.TotalRecords);

            if (_requestMeta != null)
                builder.Add(_requestMeta.GetMeta());

            var meta = builder.Build();
            if (meta.Count > 0) return meta;
            return null;
        }

        private bool ShouldIncludePageLinks(ContextEntity entity) => entity.Links.HasFlag(Link.Paging);

        private List<DocumentData> AppendIncludedObject(List<DocumentData> includedObject, ContextEntity contextEntity, IIdentifiable entity)
        {
            var includedEntities = GetIncludedEntities(includedObject, contextEntity, entity);
            if (includedEntities?.Count > 0)
            {
                includedObject = includedEntities;
            }

            return includedObject;
        }

        public DocumentData GetData(ContextEntity contextEntity, IIdentifiable entity)
        {
            var data = new DocumentData
            {
                Type = contextEntity.EntityName,
                Id = entity.StringId
            };

            if (_jsonApiContext.IsRelationshipData)
                return data;

            data.Attributes = new Dictionary<string, object>();

            contextEntity.Attributes.ForEach(attr =>
            {
                var attributeValue = attr.GetValue(entity);
                if (ShouldIncludeAttribute(attr, attributeValue))
                {
                    data.Attributes.Add(attr.PublicAttributeName, attributeValue);
                }
            });

            if (contextEntity.Relationships.Count > 0)
                AddRelationships(data, contextEntity, entity);

            return data;
        }

        private bool ShouldIncludeAttribute(AttrAttribute attr, object attributeValue)
        {
            return OmitNullValuedAttribute(attr, attributeValue) == false
                   && ((_jsonApiContext.QuerySet == null
                       || _jsonApiContext.QuerySet.Fields.Count == 0)
                       || _jsonApiContext.QuerySet.Fields.Contains(attr.InternalAttributeName));
        }

        private bool OmitNullValuedAttribute(AttrAttribute attr, object attributeValue)
        {
            return attributeValue == null && _documentBuilderOptions.OmitNullValuedAttributes;
        }

        private void AddRelationships(DocumentData data, ContextEntity contextEntity, IIdentifiable entity)
        {
            data.Relationships = new Dictionary<string, RelationshipData>();
            var linkBuilder = new LinkBuilder(_jsonApiContext);

            contextEntity.Relationships.ForEach(r =>
            {
                var relationshipData = new RelationshipData();

                if (r.DocumentLinks.HasFlag(Link.None) == false)
                {
                    relationshipData.Links = new Links();
                    if (r.DocumentLinks.HasFlag(Link.Self))
                        relationshipData.Links.Self = linkBuilder.GetSelfRelationLink(contextEntity.EntityName, entity.StringId, r.PublicRelationshipName);

                    if (r.DocumentLinks.HasFlag(Link.Related))
                        relationshipData.Links.Related = linkBuilder.GetRelatedRelationLink(contextEntity.EntityName, entity.StringId, r.PublicRelationshipName);
                }
                
                var navigationEntity = _jsonApiContext.ContextGraph
                    .GetRelationship(entity, r.InternalRelationshipName);

                if (navigationEntity == null)
                    relationshipData.SingleData = null;
                else if (navigationEntity is IEnumerable)
                    relationshipData.ManyData = GetRelationships((IEnumerable<object>)navigationEntity);
                else
                    relationshipData.SingleData = GetRelationship(navigationEntity);
                
                data.Relationships.Add(r.PublicRelationshipName, relationshipData);
            });
        }

        private List<DocumentData> GetIncludedEntities(List<DocumentData> included, ContextEntity contextEntity, IIdentifiable entity)
        {
            contextEntity.Relationships.ForEach(r =>
            {
                if (!RelationshipIsIncluded(r.PublicRelationshipName)) return;

                var navigationEntity = _jsonApiContext.ContextGraph.GetRelationship(entity, r.InternalRelationshipName);

                if (navigationEntity is IEnumerable hasManyNavigationEntity)
                    foreach (IIdentifiable includedEntity in hasManyNavigationEntity)
                        included = AddIncludedEntity(included, includedEntity);
                else
                    included = AddIncludedEntity(included, (IIdentifiable)navigationEntity);
            });

            return included;
        }

        private List<DocumentData> AddIncludedEntity(List<DocumentData> entities, IIdentifiable entity)
        {
            var includedEntity = GetIncludedEntity(entity);

            if (entities == null)
                entities = new List<DocumentData>();

            if (includedEntity != null && entities.Any(doc =>
                string.Equals(doc.Id, includedEntity.Id) && string.Equals(doc.Type, includedEntity.Type)) == false)
            {
                entities.Add(includedEntity);
            }

            return entities;
        }

        private DocumentData GetIncludedEntity(IIdentifiable entity)
        {
            if (entity == null) return null;

            var contextEntity = _jsonApiContext.ContextGraph.GetContextEntity(entity.GetType());

            var data = GetData(contextEntity, entity);

            data.Attributes = new Dictionary<string, object>();

            contextEntity.Attributes.ForEach(attr =>
            {
                data.Attributes.Add(attr.PublicAttributeName, attr.GetValue(entity));
            });

            return data;
        }

        private bool RelationshipIsIncluded(string relationshipName)
        {
            return _jsonApiContext.IncludedRelationships != null &&
                _jsonApiContext.IncludedRelationships.Contains(relationshipName);
        }

        private List<ResourceIdentifierObject> GetRelationships(IEnumerable<object> entities)
        {
            var objType = entities.GetElementType();

            var typeName = _jsonApiContext.ContextGraph.GetContextEntity(objType);

            var relationships = new List<ResourceIdentifierObject>();
            foreach (var entity in entities)
            {
                relationships.Add(new ResourceIdentifierObject {
                    Type = typeName.EntityName,
                    Id = ((IIdentifiable)entity).StringId
                });
            }
            return relationships;
        }
        private ResourceIdentifierObject GetRelationship(object entity)
        {
            var objType = entity.GetType();

            var typeName = _jsonApiContext.ContextGraph.GetContextEntity(objType);

            return new ResourceIdentifierObject {
                Type = typeName.EntityName,
                Id = ((IIdentifiable)entity).StringId
            };
        }
    }
}
