using System;
using System.Threading.Tasks;
using JsonApiDotNetCore.Internal;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.DependencyInjection;

namespace JsonApiDotNetCore.Formatters
{
    public sealed class JsonApiInputFormatter : IInputFormatter
    {
        public bool CanRead(InputFormatterContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var contentTypeString = context.HttpContext.Request.ContentType;

            return contentTypeString == HeaderConstants.ContentType;
        }

        public async Task<InputFormatterResult> ReadAsync(InputFormatterContext context)
        {
            var reader = context.HttpContext.RequestServices.GetService<IJsonApiReader>();
            return await reader.ReadAsync(context);
        }
    }
}
