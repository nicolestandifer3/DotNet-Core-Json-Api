using System;
using System.Collections.Generic;

namespace JsonApiDotNetCore.Internal
{
    public class ContextEntity
    {
        public string EntityName { get; set; }
        public Type EntityType { get; set; }
        public List<Relationship> Relationships { get; set; }
    }
}
