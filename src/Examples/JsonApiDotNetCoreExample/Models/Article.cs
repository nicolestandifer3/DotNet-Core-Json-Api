using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using JsonApiDotNetCore.Models;

namespace JsonApiDotNetCoreExample.Models
{
    public sealed class Article : Identifiable
    {
        [Attr]
        public string Name { get; set; }

        [HasOne]
        public Author Author { get; set; }
        public int AuthorId { get; set; }

        [NotMapped]
        [HasManyThrough(nameof(ArticleTags))]
        public List<Tag> Tags { get; set; }
        public List<ArticleTag> ArticleTags { get; set; }


        [NotMapped]
        [HasManyThrough(nameof(IdentifiableArticleTags))]
        public List<Tag> IdentifiableTags { get; set; }
        public List<IdentifiableArticleTag> IdentifiableArticleTags { get; set; }
    }
}
