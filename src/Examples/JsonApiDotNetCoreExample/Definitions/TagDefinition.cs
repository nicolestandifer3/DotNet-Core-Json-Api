using System.Collections.Generic;
using System.Linq;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Hooks.Internal.Execution;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCoreExample.Models;

namespace JsonApiDotNetCoreExample.Definitions
{
    public class TagDefinition : ResourceDefinition<Tag>
    {
        public TagDefinition(IResourceGraph resourceGraph) : base(resourceGraph) { }

        public override IEnumerable<Tag> BeforeCreate(IResourceHashSet<Tag> affected, ResourcePipeline pipeline)
        {
            return base.BeforeCreate(affected, pipeline);
        }

        public override IEnumerable<Tag> OnReturn(HashSet<Tag> resources, ResourcePipeline pipeline)
        {
            return resources.Where(t => t.Name != "This should not be included");
        }
    }
}
