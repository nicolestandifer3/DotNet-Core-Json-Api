using JsonApiDotNetCore.Graph;
using JsonApiDotNetCore.Models.Links;
using Newtonsoft.Json;

namespace JsonApiDotNetCore.Configuration
{
    /// <summary>
    /// Global options
    /// </summary>
    public class JsonApiOptions : IJsonApiOptions
    {

        /// <inheritdoc/>
        public bool RelativeLinks { get; set; } = false;

        /// <inheritdoc/>
        public Link TopLevelLinks { get; set; } = Link.All;

        /// <inheritdoc/>
        public Link ResourceLinks { get; set; } = Link.All;

        /// <inheritdoc/>
        public Link RelationshipLinks { get; set; } = Link.All;

        /// <summary>
        /// Provides an interface for formatting relationship id properties given the navigation property name
        /// </summary>
        public static IRelatedIdMapper RelatedIdMapper { get; set; } = new DefaultRelatedIdMapper();

        /// <summary>
        /// Whether or not stack traces should be serialized in Error objects
        /// </summary>
        public static bool DisableErrorStackTraces { get; set; } = true;

        /// <summary>
        /// Whether or not source URLs should be serialized in Error objects
        /// </summary>
        public static bool DisableErrorSource { get; set; } = true;

        /// <summary>
        /// Whether or not ResourceHooks are enabled. 
        /// 
        /// Default is set to <see langword="true"/>
        /// </summary>
        public bool EnableResourceHooks { get; set; } = true;

        /// <summary>
        /// Whether or not database values should be included by default
        /// for resource hooks. Ignored if EnableResourceHooks is set false.
        /// 
        /// Defaults to <see langword="false"/>.
        /// </summary>
        public bool LoadDatabaseValues { get; set; }

        /// <summary>
        /// The base URL Namespace
        /// </summary>
        /// <example>
        /// <code>options.Namespace = "api/v1";</code>
        /// </example>
        public string Namespace { get; set; }

        /// <summary>
        /// The default page size for all resources
        /// </summary>
        /// <example>
        /// <code>options.DefaultPageSize = 10;</code>
        /// </example>
        public int DefaultPageSize { get; set; }

        /// <summary>
        /// Optional. When set, limits the maximum page size for all resources.
        /// </summary>
        /// <example>
        /// <code>options.MaximumPageSize = 50;</code>
        /// </example>
        public int? MaximumPageSize { get; set; }

        /// <summary>
        /// Optional. When set, limits the maximum page number for all resources.
        /// </summary>
        /// <example>
        /// <code>options.MaximumPageNumber = 100;</code>
        /// </example>
        public int? MaximumPageNumber { get; set; }

        /// <summary>
        /// Whether or not the total-record count should be included in all document
        /// level meta objects.
        /// Defaults to false.
        /// </summary>
        /// <example>
        /// <code>options.IncludeTotalRecordCount = true;</code>
        /// </example>
        public bool IncludeTotalRecordCount { get; set; }

        /// <summary>
        /// Whether or not clients can provide ids when creating resources.
        /// Defaults to false.  When disabled the application will respond 
        /// with a 403 Forbidden response if a client attempts to create a 
        /// resource with a defined id.
        /// </summary>
        /// <example>
        /// <code>options.AllowClientGeneratedIds = true;</code>
        /// </example>
        public bool AllowClientGeneratedIds { get; set; }

        /// <summary>
        /// Whether or not to allow all custom query parameters.
        /// </summary>
        /// <example>
        /// <code>
        /// options.AllowCustomQueryParameters = true;
        /// </code>
        /// </example>
        public bool AllowCustomQueryParameters { get; set; }

        /// <summary>
        /// The default behavior for serializing null attributes.
        /// </summary>
        /// <example>
        /// <code>
        /// options.NullAttributeResponseBehavior = new NullAttributeResponseBehavior {
        ///  // ...
        ///};
        /// </code>
        /// </example>
        public NullAttributeResponseBehavior NullAttributeResponseBehavior { get; set; }
        public DefaultAttributeResponseBehavior DefaultAttributeResponseBehavior { get; set; }

        /// <summary>
        /// Whether or not to validate model state.
        /// </summary>
        /// <example>
        /// <code>
        /// options.ValidateModelState = true;
        /// </code>
        /// </example>
        public bool ValidateModelState { get; set; }

        public JsonSerializerSettings SerializerSettings { get; } = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore
        };
    }
}
