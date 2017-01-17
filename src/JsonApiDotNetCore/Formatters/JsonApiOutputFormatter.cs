using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JsonApiDotNetCore.Internal;
using JsonApiDotNetCore.Serialization;
using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace JsonApiDotNetCore.Formatters
{
    public class JsonApiOutputFormatter : IOutputFormatter
    {
        public bool CanWriteResult(OutputFormatterCanWriteContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var contentTypeString = context.HttpContext.Request.ContentType;

            return string.IsNullOrEmpty(contentTypeString) || contentTypeString == "application/vnd.api+json";
        }

        public async Task WriteAsync(OutputFormatterWriteContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var response = context.HttpContext.Response;

            using (var writer = context.WriterFactory(response.Body, Encoding.UTF8))
            {
                var jsonApiContext = context.HttpContext.RequestServices.GetService<IJsonApiContext>();

                string responseContent;
                try
                {
                    if(context.Object.GetType() == typeof(Error))
                        responseContent = JsonConvert.SerializeObject(context.Object);
                    else
                        responseContent = JsonApiSerializer.Serialize(context.Object, jsonApiContext);
                }
                catch(Exception e)
                {
                    responseContent = new Error("400", e.Message).GetJson();
                    response.StatusCode = 400;
                }

                await writer.WriteAsync(responseContent);
                await writer.FlushAsync();       
            }
        }
    }
}
