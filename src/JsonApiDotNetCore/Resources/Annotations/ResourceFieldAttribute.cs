using System;
using System.Reflection;

namespace JsonApiDotNetCore.Resources.Annotations
{
    /// <summary>
    /// Used to expose a property on a resource class as a json:api field (attribute or relationship).
    /// See https://jsonapi.org/format/#document-resource-object-fields.
    /// </summary>
    public abstract class ResourceFieldAttribute : Attribute
    {
        private string _publicName;

        /// <summary>
        /// The publicly exposed name of this json:api field.
        /// When not explicitly assigned, the configured casing convention is applied on the property name.
        /// </summary>
        public string PublicName
        {
            get => _publicName;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new ArgumentException("Exposed name cannot be null, empty or contain only whitespace.", nameof(value));
                }
                _publicName = value;
            }
        }

        /// <summary>
        /// The resource property that this attribute is declared on.
        /// </summary>
        public PropertyInfo Property { get; internal set; }

        public override string ToString()
        {
            return PublicName ?? (Property != null ? Property.Name : base.ToString());
        }
    }
}
