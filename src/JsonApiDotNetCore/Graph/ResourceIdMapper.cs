namespace JsonApiDotNetCore.Graph
{
    /// <summary>
    /// Provides an interface for formatting relationship identifiers from the navigation property name
    /// </summary>
    public interface IRelatedIdMapper
    {
        /// <summary>
        /// Get the internal property name for the database mapped identifier property
        /// </summary>
        ///
        /// <example>
        /// <code>
        /// DefaultResourceNameFormatter.FormatId("Article");
        /// // "ArticleId"
        /// </code>
        /// </example>
        string GetRelatedIdPropertyName(string propertyName);
    }

    /// <inheritdoc />
    public class DefaultRelatedIdMapper : IRelatedIdMapper
    {
        /// <inheritdoc />
        public string GetRelatedIdPropertyName(string propertyName) => propertyName + "Id";
    }
}