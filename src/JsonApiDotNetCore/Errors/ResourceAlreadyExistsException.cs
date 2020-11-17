using System.Net;
using JsonApiDotNetCore.Serialization.Objects;

namespace JsonApiDotNetCore.Errors
{
    /// <summary>
    /// The error that is thrown when creating a resource with an ID that already exists.
    /// </summary>
    public sealed class ResourceAlreadyExistsException : JsonApiException
    {
        public ResourceAlreadyExistsException(string resourceId, string resourceType)
            : base(new Error(HttpStatusCode.Conflict)
            {
                Title = "Another resource with the specified ID already exists.",
                Detail = $"Another resource of type '{resourceType}' with ID '{resourceId}' already exists."
            })
        {
        }
    }
}
