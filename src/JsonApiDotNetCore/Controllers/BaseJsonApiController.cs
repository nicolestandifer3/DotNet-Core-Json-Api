using System;
using System.Net.Http;
using System.Threading.Tasks;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Errors;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace JsonApiDotNetCore.Controllers
{
    /// <summary>
    /// Implements the foundational ASP.NET Core controller layer in the JsonApiDotNetCore architecture that delegates to a Resource Service.
    /// </summary>
    /// <typeparam name="TResource">The resource type.</typeparam>
    /// <typeparam name="TId">The resource identifier type.</typeparam>
    public abstract class BaseJsonApiController<TResource, TId> : CoreJsonApiController where TResource : class, IIdentifiable<TId>
    {
        private readonly IJsonApiOptions _options;
        private readonly IGetAllService<TResource, TId> _getAll;
        private readonly IGetByIdService<TResource, TId> _getById;
        private readonly IGetSecondaryService<TResource, TId> _getSecondary;
        private readonly IGetRelationshipService<TResource, TId> _getRelationship;
        private readonly ICreateService<TResource, TId> _create;
        private readonly IUpdateService<TResource, TId> _update;
        private readonly IUpdateRelationshipService<TResource, TId> _updateRelationships;
        private readonly IDeleteService<TResource, TId> _delete;
        private readonly TraceLogWriter<BaseJsonApiController<TResource, TId>> _traceWriter;

        /// <summary>
        /// Creates an instance from a read/write service.
        /// </summary>
        protected BaseJsonApiController(
            IJsonApiOptions options,
            ILoggerFactory loggerFactory,
            IResourceService<TResource, TId> resourceService)
            : this(options, loggerFactory, resourceService, resourceService, resourceService, resourceService,
                resourceService, resourceService, resourceService, resourceService)
        { }

        /// <summary>
        /// Creates an instance from separate services for reading and writing.
        /// </summary>
        protected BaseJsonApiController(
            IJsonApiOptions options,
            ILoggerFactory loggerFactory,
            IResourceQueryService<TResource, TId> queryService = null,
            IResourceCommandService<TResource, TId> commandService = null)
            : this(options, loggerFactory, queryService, queryService, queryService, queryService, commandService,
                commandService, commandService, commandService)
        { }

        /// <summary>
        /// Creates an instance from separate services for the various individual read and write methods.
        /// </summary>
        protected BaseJsonApiController(
            IJsonApiOptions options,
            ILoggerFactory loggerFactory,
            IGetAllService<TResource, TId> getAll = null,
            IGetByIdService<TResource, TId> getById = null,
            IGetSecondaryService<TResource, TId> getSecondary = null,
            IGetRelationshipService<TResource, TId> getRelationship = null,
            ICreateService<TResource, TId> create = null,
            IUpdateService<TResource, TId> update = null,
            IUpdateRelationshipService<TResource, TId> updateRelationships = null,
            IDeleteService<TResource, TId> delete = null)
        {
            if (loggerFactory == null) throw new ArgumentNullException(nameof(loggerFactory));

            _options = options ?? throw new ArgumentNullException(nameof(options));
            _traceWriter = new TraceLogWriter<BaseJsonApiController<TResource, TId>>(loggerFactory);
            _getAll = getAll;
            _getById = getById;
            _getSecondary = getSecondary;
            _getRelationship = getRelationship;
            _create = create;
            _update = update;
            _updateRelationships = updateRelationships;
            _delete = delete;
        }

        /// <summary>
        /// Gets a collection of top-level (non-nested) resources.
        /// Example: GET /articles HTTP/1.1
        /// </summary>
        public virtual async Task<IActionResult> GetAsync()
        {
            _traceWriter.LogMethodStart();

            if (_getAll == null) throw new RequestMethodNotAllowedException(HttpMethod.Get);
            var resources = await _getAll.GetAsync();
            return Ok(resources);
        }

        /// <summary>
        /// Gets a single top-level (non-nested) resource by ID.
        /// Example: /articles/1
        /// </summary>
        public virtual async Task<IActionResult> GetAsync(TId id)
        {
            _traceWriter.LogMethodStart(new {id});

            if (_getById == null) throw new RequestMethodNotAllowedException(HttpMethod.Get);
            var resource = await _getById.GetAsync(id);
            return Ok(resource);
        }

        /// <summary>
        /// Gets a single resource relationship.
        /// Example: GET /articles/1/relationships/author HTTP/1.1
        /// </summary>
        public virtual async Task<IActionResult> GetRelationshipAsync(TId id, string relationshipName)
        {
            _traceWriter.LogMethodStart(new {id, relationshipName});
            if (relationshipName == null) throw new ArgumentNullException(nameof(relationshipName));

            if (_getRelationship == null) throw new RequestMethodNotAllowedException(HttpMethod.Get);
            var relationship = await _getRelationship.GetRelationshipAsync(id, relationshipName);

            return Ok(relationship);
        }

        /// <summary>
        /// Gets a single resource or multiple resources at a nested endpoint.
        /// Examples:
        /// GET /articles/1/author HTTP/1.1
        /// GET /articles/1/revisions HTTP/1.1
        /// </summary>
        public virtual async Task<IActionResult> GetSecondaryAsync(TId id, string relationshipName)
        {
            _traceWriter.LogMethodStart(new {id, relationshipName});
            if (relationshipName == null) throw new ArgumentNullException(nameof(relationshipName));

            if (_getSecondary == null) throw new RequestMethodNotAllowedException(HttpMethod.Get);
            var relationship = await _getSecondary.GetSecondaryAsync(id, relationshipName);
            return Ok(relationship);
        }

        /// <summary>
        /// Creates a new resource.
        /// </summary>
        public virtual async Task<IActionResult> PostAsync([FromBody] TResource resource)
        {
            _traceWriter.LogMethodStart(new {resource});

            if (_create == null)
                throw new RequestMethodNotAllowedException(HttpMethod.Post);

            if (resource == null)
                throw new InvalidRequestBodyException(null, null, null);

            if (!_options.AllowClientGeneratedIds && !string.IsNullOrEmpty(resource.StringId))
                throw new ResourceIdInPostRequestNotAllowedException();

            if (_options.ValidateModelState && !ModelState.IsValid)
            {
                var namingStrategy = _options.SerializerContractResolver.NamingStrategy;
                throw new InvalidModelStateException(ModelState, typeof(TResource), _options.IncludeExceptionStackTraceInErrors, namingStrategy);
            }

            resource = await _create.CreateAsync(resource);

            return Created($"{HttpContext.Request.Path}/{resource.StringId}", resource);
        }

        /// <summary>
        /// Updates an existing resource. May contain a partial set of attributes.
        /// </summary>
        public virtual async Task<IActionResult> PatchAsync(TId id, [FromBody] TResource resource)
        {
            _traceWriter.LogMethodStart(new {id, resource});

            if (_update == null) throw new RequestMethodNotAllowedException(HttpMethod.Patch);
            if (resource == null)
                throw new InvalidRequestBodyException(null, null, null);

            if (_options.ValidateModelState && !ModelState.IsValid)
            {
                var namingStrategy = _options.SerializerContractResolver.NamingStrategy;
                throw new InvalidModelStateException(ModelState, typeof(TResource), _options.IncludeExceptionStackTraceInErrors, namingStrategy);
            }

            var updated = await _update.UpdateAsync(id, resource);
            return updated == null ? Ok(null) : Ok(updated);
        }

        /// <summary>
        /// Updates a relationship.
        /// </summary>
        public virtual async Task<IActionResult> PatchRelationshipAsync(TId id, string relationshipName, [FromBody] object relationships)
        {
            _traceWriter.LogMethodStart(new {id, relationshipName, relationships});
            if (relationshipName == null) throw new ArgumentNullException(nameof(relationshipName));

            if (_updateRelationships == null) throw new RequestMethodNotAllowedException(HttpMethod.Patch);
            await _updateRelationships.UpdateRelationshipAsync(id, relationshipName, relationships);
            return Ok();
        }

        /// <summary>
        /// Deletes a resource.
        /// </summary>
        public virtual async Task<IActionResult> DeleteAsync(TId id)
        {
            _traceWriter.LogMethodStart(new {id});

            if (_delete == null) throw new RequestMethodNotAllowedException(HttpMethod.Delete);
            await _delete.DeleteAsync(id);

            return NoContent();
        }
    }

    /// <inheritdoc />
    public abstract class BaseJsonApiController<TResource> : BaseJsonApiController<TResource, int> where TResource : class, IIdentifiable<int>
    {
        /// <inheritdoc />
        protected BaseJsonApiController(
            IJsonApiOptions options,
            ILoggerFactory loggerFactory,
            IResourceService<TResource, int> resourceService)
            : base(options, loggerFactory, resourceService, resourceService)
        { }

        /// <inheritdoc />
        protected BaseJsonApiController(
            IJsonApiOptions options,
            ILoggerFactory loggerFactory,
            IResourceQueryService<TResource, int> queryService = null,
            IResourceCommandService<TResource, int> commandService = null)
            : base(options, loggerFactory, queryService, commandService)
        { }

        /// <inheritdoc />
        protected BaseJsonApiController(
            IJsonApiOptions options,
            ILoggerFactory loggerFactory,
            IGetAllService<TResource, int> getAll = null,
            IGetByIdService<TResource, int> getById = null,
            IGetSecondaryService<TResource, int> getSecondary = null,
            IGetRelationshipService<TResource, int> getRelationship = null,
            ICreateService<TResource, int> create = null,
            IUpdateService<TResource, int> update = null,
            IUpdateRelationshipService<TResource, int> updateRelationships = null,
            IDeleteService<TResource, int> delete = null)
            : base(options, loggerFactory, getAll, getById, getSecondary, getRelationship, create, update,
                updateRelationships, delete)
        { }
    }
}
