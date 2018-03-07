using System;
using System.Collections.Generic;
using JsonApiDotNetCore.Models;

namespace JsonApiDotNetCore.Internal
{
    public class ContextEntity
    {
        public string EntityName { get; set; }
        public Type EntityType { get; set; }
        public Type IdentityType { get; set; }
        public List<AttrAttribute> Attributes { get; set; }
        public List<RelationshipAttribute> Relationships { get; set; }
        public Link Links { get; set; } = Link.All;
    }
}
