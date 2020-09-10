using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Resources.Annotations;

namespace ReportsExample.Models
{
    public sealed class Report : Identifiable
    {
        [Attr]
        public string Title { get; set; }

        [Attr]
        public ReportStatistics Statistics { get; set; }
    }
}
