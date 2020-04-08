using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using JsonApiDotNetCore.Exceptions;
using JsonApiDotNetCore.Models;
using JsonApiDotNetCore.Serialization.Server;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.Logging;

namespace JsonApiDotNetCore.Formatters
{
    /// <inheritdoc />
    public class JsonApiReader : IJsonApiReader
    {
        private readonly IJsonApiDeserializer _deserializer;
        private readonly ILogger<JsonApiReader> _logger;

        public JsonApiReader(IJsonApiDeserializer deserializer,
                             ILoggerFactory loggerFactory)
        {
            _deserializer = deserializer;
            _logger = loggerFactory.CreateLogger<JsonApiReader>();
        }

        public async Task<InputFormatterResult> ReadAsync(InputFormatterContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var request = context.HttpContext.Request;
            if (request.ContentLength == 0)
            {
                return await InputFormatterResult.SuccessAsync(null);
            }

            string body = await GetRequestBody(context.HttpContext.Request.Body);

            string url = context.HttpContext.Request.GetEncodedUrl();
            _logger.LogTrace($"Received request at '{url}' with body: <<{body}>>");

            object model;
            try
            {
                model = _deserializer.Deserialize(body);
            }
            catch (InvalidRequestBodyException exception)
            {
                exception.SetRequestBody(body);
                throw;
            }
            catch (Exception exception)
            {
                throw new InvalidRequestBodyException(null, null, body, exception);
            }

            if (context.HttpContext.Request.Method == "PATCH")
            {
                var hasMissingId = model is IList list ? CheckForId(list) : CheckForId(model);
                if (hasMissingId)
                {
                    throw new InvalidRequestBodyException("Payload must include id attribute.", null, body);
                }
            }

            return await InputFormatterResult.SuccessAsync(model);
        }

        /// <summary> Checks if the deserialized payload has an ID included </summary>
        private bool CheckForId(object model)
        {
            if (model == null) return false;
            if (model is ResourceObject ro)
            {
                if (string.IsNullOrEmpty(ro.Id)) return true;
            }
            else if (model is IIdentifiable identifiable)
            {
                if (string.IsNullOrEmpty(identifiable.StringId)) return true;
            }
            return false;
        }

        /// <summary> Checks if the elements in the deserialized payload have an ID included </summary>
        private bool CheckForId(IList modelList)
        {
            foreach (var model in modelList)
            {
                if (model == null) continue;
                if (model is ResourceObject ro)
                {
                    if (string.IsNullOrEmpty(ro.Id)) return true;
                }
                else if (model is IIdentifiable identifiable)
                {
                    if (string.IsNullOrEmpty(identifiable.StringId)) return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Fetches the request from body asynchronously.
        /// </summary>
        /// <param name="body">Input stream for body</param>
        /// <returns>String content of body sent to server.</returns>
        private async Task<string> GetRequestBody(Stream body)
        {
            using var reader = new StreamReader(body);
            // This needs to be set to async because
            // Synchronous IO operations are 
            // https://github.com/aspnet/AspNetCore/issues/7644
            return await reader.ReadToEndAsync();
        }
    }
}
