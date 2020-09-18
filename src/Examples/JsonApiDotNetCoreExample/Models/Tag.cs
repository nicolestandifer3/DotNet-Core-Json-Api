using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Resources.Annotations;

namespace JsonApiDotNetCoreExample.Models
{
    public class Tag : Identifiable
    {
        [Attr]
        public string Name { get; set; }

        [Attr]
        public TagColor Color { get; set; }
    }

    public enum TagColor
    {
        Red,
        Green,
        Blue
    }
}
