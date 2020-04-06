using System;
using System.Net;
using JsonApiDotNetCore.Models.JsonApiDocuments;
using Newtonsoft.Json;

namespace JsonApiDotNetCore.Exceptions
{
    public class JsonApiException : Exception
    {
        public Error Error { get; }

        public JsonApiException(Error error, Exception innerException = null)
            : base(error.Title, innerException)
        {
            Error = error;
        }

        public JsonApiException(HttpStatusCode status, string message)
            : base(message)
        {
            Error = new Error(status)
            {
                Title = message
            };
        }

        public override string Message => "Error = " + JsonConvert.SerializeObject(Error, Formatting.Indented);
    }
}
