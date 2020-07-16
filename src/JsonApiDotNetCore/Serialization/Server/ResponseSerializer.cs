using System;
using System.Collections.Generic;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Extensions;
using JsonApiDotNetCore.Models;
using Newtonsoft.Json;
using JsonApiDotNetCore.Models.Annotation;
using JsonApiDotNetCore.Serialization.Server.Builders;
using JsonApiDotNetCore.Models.JsonApiDocuments;
using JsonApiDotNetCore.RequestServices.Contracts;

namespace JsonApiDotNetCore.Serialization.Server
{
    /// <summary>
    /// Server serializer implementation of <see cref="BaseDocumentBuilder"/>
    /// </summary>
    /// <remarks>
    /// Because in JsonApiDotNetCore every json:api request is associated with exactly one
    /// resource (the primary resource, see <see cref="ICurrentRequest.PrimaryResource"/>),
    /// the serializer can leverage this information using generics.
    /// See <see cref="ResponseSerializerFactory"/> for how this is instantiated.
    /// </remarks>
    /// <typeparam name="TResource">Type of the resource associated with the scope of the request
    /// for which this serializer is used.</typeparam>
    public class ResponseSerializer<TResource> : BaseDocumentBuilder, IJsonApiSerializer, IResponseSerializer
        where TResource : class, IIdentifiable
    {
        public RelationshipAttribute RequestRelationship { get; set; }
        private readonly IFieldsToSerialize _fieldsToSerialize;
        private readonly IJsonApiOptions _options;
        private readonly IMetaBuilder<TResource> _metaBuilder;
        private readonly Type _primaryResourceType;
        private readonly ILinkBuilder _linkBuilder;
        private readonly IIncludedResourceObjectBuilder _includedBuilder;

        public ResponseSerializer(IMetaBuilder<TResource> metaBuilder,
            ILinkBuilder linkBuilder,
            IIncludedResourceObjectBuilder includedBuilder,
            IFieldsToSerialize fieldsToSerialize,
            IResourceObjectBuilder resourceObjectBuilder,
            IJsonApiOptions options)
            : base(resourceObjectBuilder)
        {
            _fieldsToSerialize = fieldsToSerialize;
            _options = options;
            _linkBuilder = linkBuilder;
            _metaBuilder = metaBuilder;
            _includedBuilder = includedBuilder;
            _primaryResourceType = typeof(TResource);
        }

        /// <inheritdoc/>
        public string Serialize(object data)
        {
            if (data == null || data is IIdentifiable)
            {
                return SerializeSingle((IIdentifiable)data);
            }

            if (data is IEnumerable<IIdentifiable> collectionOfIdentifiable)
            {
                return SerializeMany(collectionOfIdentifiable);
            }

            if (data is ErrorDocument errorDocument)
            {
                return SerializeErrorDocument(errorDocument);
            }

            throw new InvalidOperationException("Data being returned must be errors or resources.");
        }

        private string SerializeErrorDocument(ErrorDocument errorDocument)
        {
            return SerializeObject(errorDocument, _options.SerializerSettings, serializer => { serializer.ApplyErrorSettings(); });
        }

        /// <summary>
        /// Convert a single resource into a serialized <see cref="Document"/>
        /// </summary>
        /// <remarks>
        /// This method is set internal instead of private for easier testability.
        /// </remarks>
        internal string SerializeSingle(IIdentifiable resource)
        {
            if (RequestRelationship != null && resource != null)
            {
                var relationship = ((ResponseResourceObjectBuilder)_resourceObjectBuilder).Build(resource, RequestRelationship);
                return SerializeObject(relationship, _options.SerializerSettings, serializer => { serializer.NullValueHandling = NullValueHandling.Include; });
            }

            var (attributes, relationships) = GetFieldsToSerialize();
            var document = Build(resource, attributes, relationships);
            var resourceObject = document.SingleData;
            if (resourceObject != null)
                resourceObject.Links = _linkBuilder.GetResourceLinks(resourceObject.Type, resourceObject.Id);

            AddTopLevelObjects(document);

            return SerializeObject(document, _options.SerializerSettings, serializer => { serializer.NullValueHandling = NullValueHandling.Include; });
        }

        private (IReadOnlyCollection<AttrAttribute>, IReadOnlyCollection<RelationshipAttribute>) GetFieldsToSerialize()
        {
            return (_fieldsToSerialize.GetAttributes(_primaryResourceType), _fieldsToSerialize.GetRelationships(_primaryResourceType));
        }

        /// <summary>
        /// Convert a list of resources into a serialized <see cref="Document"/>
        /// </summary>
        /// <remarks>
        /// This method is set internal instead of private for easier testability.
        /// </remarks>
        internal string SerializeMany(IEnumerable<IIdentifiable> resources)
        {
            var (attributes, relationships) = GetFieldsToSerialize();
            var document = Build(resources, attributes, relationships);
            foreach (ResourceObject resourceObject in document.ManyData)
            {
                var links = _linkBuilder.GetResourceLinks(resourceObject.Type, resourceObject.Id);
                if (links == null)
                    break;

                resourceObject.Links = links;
            }

            AddTopLevelObjects(document);

            return SerializeObject(document, _options.SerializerSettings, serializer => { serializer.NullValueHandling = NullValueHandling.Include; });
        }

        /// <summary>
        /// Adds top-level objects that are only added to a document in the case
        /// of server-side serialization.
        /// </summary>
        private void AddTopLevelObjects(Document document)
        {
            document.Links = _linkBuilder.GetTopLevelLinks();
            document.Meta = _metaBuilder.GetMeta();
            document.Included = _includedBuilder.Build();
        }
    }
}
