using System.Threading.Tasks;
using JsonApiDotNetCore.Resources;

namespace JsonApiDotNetCore.Services
{
    /// <inheritdoc />
    public interface IGetRelationshipService<TResource> : IGetRelationshipService<TResource, int>
        where TResource : class, IIdentifiable<int>
    { }

    /// <summary />
    public interface IGetRelationshipService<TResource, in TId>
        where TResource : class, IIdentifiable<TId>
    {
        /// <summary>
        /// Handles a json:api request to retrieve a single relationship.
        /// </summary>
        Task<TResource> GetRelationshipAsync(TId id, string relationshipName);
    }
}
