using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using JsonApiDotNetCore.Internal;
using JsonApiDotNetCore.Models;
using JsonApiDotNetCore.Extensions;
using System.Linq;

namespace JsonApiDotNetCore.Builders
{
    public class ContextGraphBuilder : IContextGraphBuilder
    {
        private List<ContextEntity> _entities = new List<ContextEntity>();
        private bool _usesDbContext;
        public Link DocumentLinks { get; set; } = Link.All;

        public IContextGraph Build()
        {
            // this must be done at build so that call order doesn't matter
            _entities.ForEach(e => e.Links = GetLinkFlags(e.EntityType));

            var graph = new ContextGraph()
            {
                Entities = _entities,
                UsesDbContext = _usesDbContext
            };
            return graph;
        }

        public IContextGraphBuilder AddResource<TResource>(string pluralizedTypeName) where TResource : class, IIdentifiable<int>
            => AddResource<TResource, int>(pluralizedTypeName);

        public IContextGraphBuilder AddResource<TResource, TId>(string pluralizedTypeName) where TResource : class, IIdentifiable<TId>
        {
            var entityType = typeof(TResource);

            AssertEntityIsNotAlreadyDefined(entityType);

            _entities.Add(GetEntity(pluralizedTypeName, entityType, typeof(TId)));

            return this;
        }

        private ContextEntity GetEntity(string pluralizedTypeName, Type entityType, Type idType) => new ContextEntity
        {
            EntityName = pluralizedTypeName,
            EntityType = entityType,
            IdentityType = idType,
            Attributes = GetAttributes(entityType),
            Relationships = GetRelationships(entityType)
        };

        private Link GetLinkFlags(Type entityType)
        {
            var attribute = (LinksAttribute)entityType.GetTypeInfo().GetCustomAttribute(typeof(LinksAttribute));
            if (attribute != null)
                return attribute.Links;

            return DocumentLinks;
        }

        protected virtual List<AttrAttribute> GetAttributes(Type entityType)
        {
            var attributes = new List<AttrAttribute>();

            var properties = entityType.GetProperties();

            foreach (var prop in properties)
            {
                var attribute = (AttrAttribute)prop.GetCustomAttribute(typeof(AttrAttribute));
                if (attribute == null) continue;
                attribute.InternalAttributeName = prop.Name;
                attributes.Add(attribute);
            }
            return attributes;
        }

        protected virtual List<RelationshipAttribute> GetRelationships(Type entityType)
        {
            var attributes = new List<RelationshipAttribute>();

            var properties = entityType.GetProperties();

            foreach (var prop in properties)
            {
                var attribute = (RelationshipAttribute)prop.GetCustomAttribute(typeof(RelationshipAttribute));
                if (attribute == null) continue;
                attribute.InternalRelationshipName = prop.Name;
                attribute.Type = GetRelationshipType(attribute, prop);
                attributes.Add(attribute);
            }
            return attributes;
        }

        protected virtual Type GetRelationshipType(RelationshipAttribute relation, PropertyInfo prop)
        {
            if (relation.IsHasMany)
                return prop.PropertyType.GetGenericArguments()[0];
            else
                return prop.PropertyType;
        }

        public IContextGraphBuilder AddDbContext<T>() where T : DbContext
        {
            _usesDbContext = true;

            var contextType = typeof(T);

            var contextProperties = contextType.GetProperties();

            foreach (var property in contextProperties)
            {
                var dbSetType = property.PropertyType;

                if (dbSetType.GetTypeInfo().IsGenericType
                    && dbSetType.GetGenericTypeDefinition() == typeof(DbSet<>))
                {
                    var entityType = dbSetType.GetGenericArguments()[0];

                    AssertEntityIsNotAlreadyDefined(entityType);

                    _entities.Add(GetEntity(GetResourceName(property), entityType, GetIdType(entityType)));
                }
            }

            return this;
        }

        private string GetResourceName(PropertyInfo property)
        {
            var resourceAttribute = property.GetCustomAttribute(typeof(ResourceAttribute));
            if (resourceAttribute == null)
                return property.Name.Dasherize();

            return ((ResourceAttribute)resourceAttribute).ResourceName;
        }

        private Type GetIdType(Type resourceType)
        {
            var interfaces = resourceType.GetInterfaces();
            foreach (var type in interfaces)
            {
                if (type.GetTypeInfo().IsGenericType && type.GetGenericTypeDefinition() == typeof(IIdentifiable<>))
                    return type.GetGenericArguments()[0];
            }

            throw new ArgumentException("Type does not implement 'IIdentifiable<TId>'", nameof(resourceType));
        }

        private void AssertEntityIsNotAlreadyDefined(Type entityType)
        {
            if (_entities.Any(e => e.EntityType == entityType))
                throw new InvalidOperationException($"Cannot add entity type {entityType} to context graph, there is already an entity of that type configured.");
        }
    }
}
