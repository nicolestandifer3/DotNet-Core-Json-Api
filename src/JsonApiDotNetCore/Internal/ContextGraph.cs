using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JsonApiDotNetCore.Models;

namespace JsonApiDotNetCore.Internal
{
    public interface IContextGraph
    {
        /// <summary>
        /// Gets the value of the navigation property, defined by the relationshipName,
        /// on the provided instance.
        /// </summary>
        /// <param name="resource">The resource instance</param>
        /// <param name="propertyName">The navigation property name.</param>
        /// <example>
        /// <code>
        /// _graph.GetRelationship(todoItem, nameof(TodoItem.Owner));
        /// </code>
        /// <remarks>
        /// This method will not work with HasManyThrough relationships. 
        /// You should instead use the <see cref="GetRelationshipValue" /> method instead.
        /// </remarks>
        /// </example>
        object GetRelationship<TParent>(TParent resource, string propertyName);

        /// <summary>
        /// Gets the value of the navigation property, defined by the relationshipName,
        /// on the provided instance.
        /// </summary>
        /// <param name="resource">The resource instance</param>
        /// <param name="relationship">The attribute used to define the relationship.</param>
        /// <example>
        /// <code>
        /// _graph.GetRelationshipValue(todoItem, nameof(TodoItem.Owner));
        /// </code>
        /// </example>
        object GetRelationshipValue<TParent>(TParent resource, RelationshipAttribute relationship) where TParent : IIdentifiable;

        /// <summary>
        /// Get the internal navigation property name for the specified public
        /// relationship name.
        /// </summary>
        /// <param name="relationshipName">The public relationship name specified by a <see cref="HasOneAttribute" /> or <see cref="HasManyAttribute" /></param>
        /// <example>
        /// <code>
        /// _graph.GetRelationshipName&lt;TodoItem&gt;("achieved-date");
        /// // returns "AchievedDate"
        /// </code>
        /// </example>
        string GetRelationshipName<TParent>(string relationshipName);

        /// <summary>
        /// Get the resource metadata by the DbSet property name
        /// </summary>
        ContextEntity GetContextEntity(string dbSetName);

        /// <summary>
        /// Get the resource metadata by the resource type
        /// </summary>
        ContextEntity GetContextEntity(Type entityType);

        /// <summary>
        /// Get the public attribute name for a type based on the internal attribute name.
        /// </summary>
        /// <param name="internalAttributeName">The internal attribute name for a <see cref="Attr" />.</param>
        string GetPublicAttributeName<TParent>(string internalAttributeName);

        /// <summary>
        /// Was built against an EntityFrameworkCore DbContext ?
        /// </summary>
        bool UsesDbContext { get; }
    }

    public class ContextGraph : IContextGraph
    {
        internal List<ContextEntity> Entities { get; }
        internal List<ValidationResult> ValidationResults { get; }
        internal static IContextGraph Instance { get; set; }

        public ContextGraph() { }
        public ContextGraph(List<ContextEntity> entities, bool usesDbContext)
        {
            Entities = entities;
            UsesDbContext = usesDbContext;
            ValidationResults = new List<ValidationResult>();
            Instance = this;
        }

        // eventually, this is the planned public constructor
        // to avoid breaking changes, we will be leaving the original constructor in place
        // until the context graph validation process is completed
        // you can track progress on this issue here: https://github.com/json-api-dotnet/JsonApiDotNetCore/issues/170
        internal ContextGraph(List<ContextEntity> entities, bool usesDbContext, List<ValidationResult> validationResults)
        {
            Entities = entities;
            UsesDbContext = usesDbContext;
            ValidationResults = validationResults;
            Instance = this;
        }

        /// </ inheritdoc>
        public bool UsesDbContext { get; }

        /// </ inheritdoc>
        public ContextEntity GetContextEntity(string entityName)
            => Entities.SingleOrDefault(e => string.Equals(e.EntityName, entityName, StringComparison.OrdinalIgnoreCase));

        /// </ inheritdoc>
        public ContextEntity GetContextEntity(Type entityType)
            => Entities.SingleOrDefault(e => e.EntityType == entityType);

        /// </ inheritdoc>
        public object GetRelationship<TParent>(TParent entity, string relationshipName)
        {
            var parentEntityType = entity.GetType();

            var navigationProperty = parentEntityType
                .GetProperties()
                .SingleOrDefault(p => string.Equals(p.Name, relationshipName, StringComparison.OrdinalIgnoreCase));

            if (navigationProperty == null)
                throw new JsonApiException(400, $"{parentEntityType} does not contain a relationship named {relationshipName}");

            return navigationProperty.GetValue(entity);
        }

        public object GetRelationshipValue<TParent>(TParent resource, RelationshipAttribute relationship) where TParent : IIdentifiable
        {
            if(relationship is HasManyThroughAttribute hasManyThroughRelationship) 
            {
                return GetHasManyThrough(resource, hasManyThroughRelationship);
            }

            return GetRelationship(resource, relationship.InternalRelationshipName);
        }

        private IEnumerable<IIdentifiable> GetHasManyThrough(IIdentifiable parent, HasManyThroughAttribute hasManyThrough)
        {
            var throughProperty = GetRelationship(parent, hasManyThrough.InternalThroughName);
            if (throughProperty is IEnumerable hasManyNavigationEntity)
            {
                foreach (var includedEntity in hasManyNavigationEntity)
                {
                    var targetValue = hasManyThrough.RightProperty.GetValue(includedEntity) as IIdentifiable;
                    yield return targetValue;
                }
            }
        }

        /// </ inheritdoc>
        public string GetRelationshipName<TParent>(string relationshipName)
        {
            var entityType = typeof(TParent);
            return Entities
                .SingleOrDefault(e => e.EntityType == entityType)
                ?.Relationships
                .SingleOrDefault(r => r.Is(relationshipName))
                ?.InternalRelationshipName;
        }

        public string GetPublicAttributeName<TParent>(string internalAttributeName)
        {
            return GetContextEntity(typeof(TParent))
                .Attributes
                .SingleOrDefault(a => a.InternalAttributeName == internalAttributeName)?
                .PublicAttributeName;
        }
    }
}
