using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JsonApiDotNetCore.Extensions;
using JsonApiDotNetCore.Graph;
using JsonApiDotNetCore.Internal;
using JsonApiDotNetCore.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JsonApiDotNetCore.Builders
{
    public interface IContextGraphBuilder
    {
        /// <summary>
        /// Construct the <see cref="ContextGraph"/>
        /// </summary>
        IContextGraph Build();

        /// <summary>
        /// Add a json:api resource
        /// </summary>
        /// <typeparam name="TResource">The resource model type</typeparam>
        /// <param name="pluralizedTypeName">The pluralized name that should be exposed by the API</param>
        IContextGraphBuilder AddResource<TResource>(string pluralizedTypeName) where TResource : class, IIdentifiable<int>;

        /// <summary>
        /// Add a json:api resource
        /// </summary>
        /// <typeparam name="TResource">The resource model type</typeparam>
        /// <typeparam name="TId">The resource model identifier type</typeparam>
        /// <param name="pluralizedTypeName">The pluralized name that should be exposed by the API</param>
        IContextGraphBuilder AddResource<TResource, TId>(string pluralizedTypeName) where TResource : class, IIdentifiable<TId>;

        /// <summary>
        /// Add a json:api resource
        /// </summary>
        /// <param name="entityType">The resource model type</param>
        /// <param name="idType">The resource model identifier type</param>
        /// <param name="pluralizedTypeName">The pluralized name that should be exposed by the API</param>
        IContextGraphBuilder AddResource(Type entityType, Type idType, string pluralizedTypeName);

        /// <summary>
        /// Add all the models that are part of the provided <see cref="DbContext" /> 
        /// that also implement <see cref="IIdentifiable"/>
        /// </summary>
        /// <typeparam name="T">The <see cref="DbContext"/> implementation type.</typeparam>
        IContextGraphBuilder AddDbContext<T>() where T : DbContext;

        /// <summary>
        /// Which links to include. Defaults to <see cref="Link.All"/>.
        /// </summary>
        Link DocumentLinks { get; set; }
    }

    public class ContextGraphBuilder : IContextGraphBuilder
    {
        private List<ContextEntity> _entities = new List<ContextEntity>();
        private List<ValidationResult> _validationResults = new List<ValidationResult>();

        private bool _usesDbContext;
        public Link DocumentLinks { get; set; } = Link.All;

        public IContextGraph Build()
        {
            // this must be done at build so that call order doesn't matter
            _entities.ForEach(e => e.Links = GetLinkFlags(e.EntityType));

            var graph = new ContextGraph(_entities, _usesDbContext, _validationResults);
            return graph;
        }

        public IContextGraphBuilder AddResource<TResource>(string pluralizedTypeName) where TResource : class, IIdentifiable<int>
            => AddResource<TResource, int>(pluralizedTypeName);

        public IContextGraphBuilder AddResource<TResource, TId>(string pluralizedTypeName) where TResource : class, IIdentifiable<TId>
            => AddResource(typeof(TResource), typeof(TId), pluralizedTypeName);

        public IContextGraphBuilder AddResource(Type entityType, Type idType, string pluralizedTypeName)
        {
            AssertEntityIsNotAlreadyDefined(entityType);

            _entities.Add(GetEntity(pluralizedTypeName, entityType, idType));

            return this;
        }

        private ContextEntity GetEntity(string pluralizedTypeName, Type entityType, Type idType) => new ContextEntity
        {
            EntityName = pluralizedTypeName,
            EntityType = entityType,
            IdentityType = idType,
            Attributes = GetAttributes(entityType),
            Relationships = GetRelationships(entityType),
            ResourceType = GetResourceDefinitionType(entityType)
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
                if (attribute == null)
                    continue;

                attribute.InternalAttributeName = prop.Name;
                attribute.PropertyInfo = prop;

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

        private Type GetResourceDefinitionType(Type entityType) => typeof(ResourceDefinition<>).MakeGenericType(entityType);

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

                    var (isJsonApiResource, idType) = GetIdType(entityType);

                    if (isJsonApiResource)
                        _entities.Add(GetEntity(GetResourceName(property), entityType, idType));
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

        private (bool isJsonApiResource, Type idType) GetIdType(Type resourceType)
        {
            var possible = TypeLocator.GetIdType(resourceType);
            if (possible.isJsonApiResource)
                return possible;

            _validationResults.Add(new ValidationResult(LogLevel.Warning, $"{resourceType} does not implement 'IIdentifiable<>'. "));

            return (false, null);
        }

        private void AssertEntityIsNotAlreadyDefined(Type entityType)
        {
            if (_entities.Any(e => e.EntityType == entityType))
                throw new InvalidOperationException($"Cannot add entity type {entityType} to context graph, there is already an entity of that type configured.");
        }
    }
}
