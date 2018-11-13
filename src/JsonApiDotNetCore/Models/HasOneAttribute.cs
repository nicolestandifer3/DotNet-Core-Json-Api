using System;
using JsonApiDotNetCore.Configuration;

namespace JsonApiDotNetCore.Models
{
    public class HasOneAttribute : RelationshipAttribute
    {
        /// <summary>
        /// Create a HasOne relational link to another entity
        /// </summary>
        /// 
        /// <param name="publicName">The relationship name as exposed by the API</param>
        /// <param name="documentLinks">Which links are available. Defaults to <see cref="Link.All"/></param>
        /// <param name="canInclude">Whether or not this relationship can be included using the <c>?include=public-name</c> query string</param>
        /// <param name="withForeignKey">The foreign key property name. Defaults to <c>"{RelationshipName}Id"</c></param>
        /// <param name="withEntity">If the entity model of this relationship refers to a different type, specify that here</param>
        /// 
        /// <example>
        /// Using an alternative foreign key:
        /// 
        /// <code>
        /// public class Article : Identifiable 
        /// {
        ///     [HasOne("author", withForeignKey: nameof(AuthorKey)]
        ///     public Author Author { get; set; }
        ///     public int AuthorKey { get; set; }
        /// }
        /// </code>
        /// 
        /// </example>
        public HasOneAttribute(string publicName = null, Link documentLinks = Link.All, bool canInclude = true, string withForeignKey = null, string withEntity = null)
        : base(publicName, documentLinks, canInclude)
        {
            _explicitIdentifiablePropertyName = withForeignKey;
            EntityPropertyName = withEntity;
        }

        private readonly string _explicitIdentifiablePropertyName;
        private readonly string _relatedEntityPropertyName;
        
        /// <summary>
        /// The independent resource identifier.
        /// </summary>
        public string IdentifiablePropertyName => string.IsNullOrWhiteSpace(_explicitIdentifiablePropertyName)
            ? JsonApiOptions.RelatedIdMapper.GetRelatedIdPropertyName(InternalRelationshipName)
            : _explicitIdentifiablePropertyName;

        /// <summary>
        /// For use in entity / resource separation when the related property is also separated
        /// </summary>
        public string EntityPropertyName { get; }

        /// <summary>
        /// Sets the value of the property identified by this attribute
        /// </summary>
        /// <param name="resource">The target object</param>
        /// <param name="newValue">The new property value</param>
        public override void SetValue(object resource, object newValue)
        {
            var propertyName = (newValue?.GetType() == Type)
                ? InternalRelationshipName
                : IdentifiablePropertyName;

            var propertyInfo = resource
                .GetType()
                .GetProperty(propertyName);

            propertyInfo.SetValue(resource, newValue);
        }

        // HACK: this will likely require boxing
        // we should be able to move some of the reflection into the ResourceGraphBuilder
        /// <summary>
        /// Gets the value of the independent identifier (e.g. Article.AuthorId)
        /// </summary>
        /// 
        /// <param name="resource">
        /// An instance of dependent resource
        /// </param>
        /// 
        /// <returns>
        /// The property value or null if the property does not exist on the model.
        /// </returns>
        internal object GetIdentifiablePropertyValue(object resource) => resource
                .GetType()
                .GetProperty(IdentifiablePropertyName)
                ?.GetValue(resource);
    }
}
