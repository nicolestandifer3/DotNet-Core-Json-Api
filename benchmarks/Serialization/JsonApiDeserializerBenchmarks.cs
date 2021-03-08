using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using BenchmarkDotNet.Attributes;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Serialization;
using JsonApiDotNetCore.Serialization.Objects;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

namespace Benchmarks.Serialization
{
    // ReSharper disable once ClassCanBeSealed.Global
    [MarkdownExporter]
    public class JsonApiDeserializerBenchmarks
    {
        private static readonly string Content = JsonConvert.SerializeObject(new Document
        {
            Data = new ResourceObject
            {
                Type = BenchmarkResourcePublicNames.Type,
                Id = "1",
                Attributes = new Dictionary<string, object>
                {
                    ["name"] = Guid.NewGuid().ToString()
                }
            }
        });

        private readonly IJsonApiDeserializer _jsonApiDeserializer;

        public JsonApiDeserializerBenchmarks()
        {
            var options = new JsonApiOptions();
            IResourceGraph resourceGraph = DependencyFactory.CreateResourceGraph(options);
            var targetedFields = new TargetedFields();
            var request = new JsonApiRequest();
            var resourceFactory = new ResourceFactory(new ServiceContainer());
            var httpContextAccessor = new HttpContextAccessor();

            _jsonApiDeserializer = new RequestDeserializer(resourceGraph, resourceFactory, targetedFields, httpContextAccessor, request, options);
        }

        [Benchmark]
        public object DeserializeSimpleObject()
        {
            return _jsonApiDeserializer.Deserialize(Content);
        }
    }
}
