namespace JsonApiDotNetCore.Graph
{
    /// <summary>
    /// Uses camelCase as formatting options in the route and request/response body.
    /// </summary>
    /// <example>
    /// <code>
    /// _default.FormatResourceName(typeof(TodoItem)).Dump(); 
    /// // > "todoItems"
    /// </code>
    /// </example>
    /// <example>
    /// Given the following property:
    /// <code>
    /// public string CompoundProperty { get; set; }
    /// </code>
    /// The public attribute will be formatted like so:
    /// <code>
    /// _default.FormatPropertyName(compoundProperty).Dump(); 
    /// // > "compoundProperty"
    /// </code>
    /// </example>
    /// <example>
    /// <code>
    /// _default.ApplyCasingConvention("TodoItems"); 
    /// // > "todoItems"
    ///
    /// _default.ApplyCasingConvention("TodoItem"); 
    /// // > "todoItem"
    /// </code>
    /// </example>
    public sealed class CamelCaseFormatter: BaseResourceNameFormatter
    {
        /// <inheritdoc/>
        public override string ApplyCasingConvention(string properName) => char.ToLower(properName[0]) + properName.Substring(1);
    }
}
