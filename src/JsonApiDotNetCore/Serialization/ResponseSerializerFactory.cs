using System;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Middleware;

namespace JsonApiDotNetCore.Serialization
{
    /// <summary>
    /// A factory class to abstract away the initialization of the serializer from the
    /// ASP.NET Core formatter pipeline.
    /// </summary>
    public class ResponseSerializerFactory : IJsonApiSerializerFactory
    {
        private readonly IServiceProvider _provider;
        private readonly IJsonApiRequest _request;

        public ResponseSerializerFactory(IJsonApiRequest request, IRequestScopedServiceProvider provider)
        {
            _request = request ?? throw new ArgumentNullException(nameof(request));
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        /// <summary>
        /// Initializes the server serializer using the <see cref="ResourceContext"/>
        /// associated with the current request.
        /// </summary>
        public IJsonApiSerializer GetSerializer()
        {
            var targetType = GetDocumentType();

            var serializerType = typeof(ResponseSerializer<>).MakeGenericType(targetType);
            var serializer = (IResponseSerializer)_provider.GetService(serializerType);
            if (_request.Kind == EndpointKind.Relationship && _request.Relationship != null)
                serializer.RequestRelationship = _request.Relationship;

            return (IJsonApiSerializer)serializer;
        }

        private Type GetDocumentType()
        {
            var resourceContext = _request.SecondaryResource ?? _request.PrimaryResource;
            return resourceContext.ResourceType;
        }
    }
}
