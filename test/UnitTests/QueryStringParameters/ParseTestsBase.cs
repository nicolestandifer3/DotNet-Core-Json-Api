using JsonApiDotNetCore.Builders;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Internal.Contracts;
using JsonApiDotNetCore.RequestServices;
using JsonApiDotNetCoreExample.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace UnitTests.QueryStringParameters
{
    public abstract class ParseTestsBase
    {
        protected JsonApiOptions Options { get; }
        protected IResourceGraph ResourceGraph { get; }
        protected CurrentRequest CurrentRequest { get; }

        protected ParseTestsBase()
        {
            Options = new JsonApiOptions();

            ResourceGraph = new ResourceGraphBuilder(Options, NullLoggerFactory.Instance)
                .AddResource<Blog>()
                .AddResource<Article>()
                .AddResource<Author>()
                .AddResource<Address>()
                .AddResource<Country>()
                .AddResource<Revision>()
                .AddResource<Tag>()
                .Build();

            CurrentRequest = new CurrentRequest
            {
                PrimaryResource = ResourceGraph.GetResourceContext<Blog>(),
                IsCollection = true
            };
        }
    }
}
