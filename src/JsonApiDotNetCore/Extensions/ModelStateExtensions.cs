using System.Linq;
using System.Net;
using System.Reflection;
using JsonApiDotNetCore.Internal;
using JsonApiDotNetCore.Models;
using JsonApiDotNetCore.Models.JsonApiDocuments;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace JsonApiDotNetCore.Extensions
{
    public static class ModelStateExtensions
    {
        public static ErrorDocument ConvertToErrorDocument<TResource>(this ModelStateDictionary modelState)
            where TResource : class, IIdentifiable
        {
            ErrorDocument document = new ErrorDocument();

            foreach (var pair in modelState.Where(x => x.Value.Errors.Any()))
            {
                var propertyName = pair.Key;
                PropertyInfo property = typeof(TResource).GetProperty(propertyName);
                string attributeName = property?.GetCustomAttribute<AttrAttribute>().PublicAttributeName ?? property?.Name;

                foreach (var modelError in pair.Value.Errors)
                {
                    if (modelError.Exception is JsonApiException jsonApiException)
                    {
                        document.Errors.Add(jsonApiException.Error);
                    }
                    else
                    {
                        document.Errors.Add(FromModelError(modelError, propertyName, attributeName));
                    }
                }
            }

            return document;
        }

        private static Error FromModelError(ModelError modelError, string propertyName, string attributeName)
        {
            var error = new Error(HttpStatusCode.UnprocessableEntity)
            {
                Title = "Input validation failed.",
                Detail = modelError.ErrorMessage,
                Source = attributeName == null ? null : new ErrorSource
                {
                    Pointer = $"/data/attributes/{attributeName}"
                },
            };

            if (modelError.Exception != null)
            {
                error.Meta = new ErrorMeta();
                error.Meta.IncludeExceptionStackTrace(modelError.Exception);
            }

            return error;
        }
    }
}
