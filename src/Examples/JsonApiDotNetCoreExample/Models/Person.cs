using System;
using System.Collections.Generic;
using JsonApiDotNetCore.Models;
using JsonApiDotNetCore.Services;

namespace JsonApiDotNetCoreExample.Models
{
    public class PersonRole : Identifiable
    {
        [HasOne("person")]
        public Person Person { get; set; }
    }

    public class Person : Identifiable, IHasMeta
    {
        [Attr("first-name")]
        public string FirstName { get; set; }

        [Attr("last-name")]
        public string LastName { get; set; }

        [Attr("age")]
        public int Age { get; set; }

        [HasMany("todo-items")]
        public virtual List<TodoItem> TodoItems { get; set; }

        [HasMany("assigned-todo-items")]
        public virtual List<TodoItem> AssignedTodoItems { get; set; }

        [HasMany("todo-collections")]
        public virtual List<TodoItemCollection> TodoItemCollections { get; set; }

        [HasOne("role")]
        public virtual PersonRole Role { get; set; }
        public int? PersonRoleId { get; set; }

        [HasOne("unincludeable-item", documentLinks: Link.All, canInclude: false)]
        public virtual TodoItem UnIncludeableItem { get; set; }

        public Dictionary<string, object> GetMeta(IJsonApiContext context)
        {
            return new Dictionary<string, object> {
                { "copyright", "Copyright 2015 Example Corp." },
                { "authors", new string[] { "Jared Nance" } }
            };
        }
    }
}
