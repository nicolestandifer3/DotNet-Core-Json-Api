using System;
using System.Collections.Generic;
using JsonApiDotNetCore.Models;

namespace JsonApiDotNetCoreExample.Models
{
    public class TodoItemCollection : Identifiable<Guid>
    {
        [Attr("name")]
        public string Name { get; set; }
        public int OwnerId { get; set; }

        [HasMany("todo-items")]
        public virtual List<TodoItem> TodoItems { get; set; }

        [HasOne("owner")]
        public virtual Person Owner { get; set; }
    }
}