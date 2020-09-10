using System;
using System.Collections.Generic;
using System.IO;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Resources.Annotations;
using JsonApiDotNetCore.Serialization.Building;
using JsonApiDotNetCore.Serialization.Objects;
using Newtonsoft.Json;

namespace JsonApiDotNetCore.Serialization
{
    /// <summary>
    /// Abstract base class for serialization.
    /// Uses <see cref="IResourceObjectBuilder"/> to convert resources into <see cref="ResourceObject"/>s and wraps them in a <see cref="Document"/>.
    /// </summary>
    public abstract class BaseSerializer
    {
        protected IResourceObjectBuilder ResourceObjectBuilder { get; }

        protected BaseSerializer(IResourceObjectBuilder resourceObjectBuilder)
        {
            ResourceObjectBuilder = resourceObjectBuilder ?? throw new ArgumentNullException(nameof(resourceObjectBuilder));
        }

        /// <summary>
        /// Builds a <see cref="Document"/> for <paramref name="resource"/>.
        /// Adds the attributes and relationships that are enlisted in <paramref name="attributes"/> and <paramref name="relationships"/>.
        /// </summary>
        /// <param name="resource">Resource to build a <see cref="ResourceObject"/> for.</param>
        /// <param name="attributes">Attributes to include in the building process.</param>
        /// <param name="relationships">Relationships to include in the building process.</param>
        /// <returns>The resource object that was built.</returns>
        protected Document Build(IIdentifiable resource, IReadOnlyCollection<AttrAttribute> attributes, IReadOnlyCollection<RelationshipAttribute> relationships)
        {
            if (resource == null)
                return new Document();

            return new Document { Data = ResourceObjectBuilder.Build(resource, attributes, relationships) };
        }

        /// <summary>
        /// Builds a <see cref="Document"/> for <paramref name="resources"/>.
        /// Adds the attributes and relationships that are enlisted in <paramref name="attributes"/> and <paramref name="relationships"/>.
        /// </summary>
        /// <param name="resources">Resource to build a <see cref="ResourceObject"/> for.</param>
        /// <param name="attributes">Attributes to include in the building process.</param>
        /// <param name="relationships">Relationships to include in the building process.</param>
        /// <returns>The resource object that was built.</returns>
        protected Document Build(IReadOnlyCollection<IIdentifiable> resources, IReadOnlyCollection<AttrAttribute> attributes, IReadOnlyCollection<RelationshipAttribute> relationships)
        {
            if (resources == null) throw new ArgumentNullException(nameof(resources));

            var data = new List<ResourceObject>();
            foreach (IIdentifiable resource in resources)
                data.Add(ResourceObjectBuilder.Build(resource, attributes, relationships));

            return new Document { Data = data };
        }

        protected string SerializeObject(object value, JsonSerializerSettings defaultSettings, Action<JsonSerializer> changeSerializer = null)
        {
            if (defaultSettings == null) throw new ArgumentNullException(nameof(defaultSettings));

            JsonSerializer serializer = JsonSerializer.CreateDefault(defaultSettings);
            changeSerializer?.Invoke(serializer);

            using var stringWriter = new StringWriter();
            using (var jsonWriter = new JsonTextWriter(stringWriter))
            {
                serializer.Serialize(jsonWriter, value);
            }

            return stringWriter.ToString();
        }
    }
}
