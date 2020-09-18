using System;
using BenchmarkDotNet.Attributes;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.QueryStrings.Internal;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Serialization;
using JsonApiDotNetCore.Serialization.Building;
using Moq;

namespace Benchmarks.Serialization
{
    [MarkdownExporter]
    public class JsonApiSerializerBenchmarks
    {
        private static readonly BenchmarkResource _content = new BenchmarkResource
        {
            Id = 123,
            Name = Guid.NewGuid().ToString()
        };

        private readonly IJsonApiSerializer _jsonApiSerializer;

        public JsonApiSerializerBenchmarks()
        {
            var options = new JsonApiOptions();
            IResourceGraph resourceGraph = DependencyFactory.CreateResourceGraph(options);
            IFieldsToSerialize fieldsToSerialize = CreateFieldsToSerialize(resourceGraph);

            var metaBuilderMock = new Mock<IMetaBuilder<BenchmarkResource>>();
            var linkBuilderMock = new Mock<ILinkBuilder>();
            var includeBuilderMock = new Mock<IIncludedResourceObjectBuilder>();

            var resourceObjectBuilder = new ResourceObjectBuilder(resourceGraph, new ResourceObjectBuilderSettings());

            _jsonApiSerializer = new ResponseSerializer<BenchmarkResource>(metaBuilderMock.Object, linkBuilderMock.Object,
                includeBuilderMock.Object, fieldsToSerialize, resourceObjectBuilder, options);
        }

        private static FieldsToSerialize CreateFieldsToSerialize(IResourceGraph resourceGraph)
        {
            var request = new JsonApiRequest();

            var constraintProviders = new IQueryConstraintProvider[]
            {
                new SparseFieldSetQueryStringParameterReader(request, resourceGraph)
            };

            var accessor = new Mock<IResourceDefinitionAccessor>().Object;

            return new FieldsToSerialize(resourceGraph, constraintProviders, accessor);
        }

        [Benchmark]
        public object SerializeSimpleObject() => _jsonApiSerializer.Serialize(_content);
    }
}
