using System;
using System.Net.Http;
using System.Threading.Tasks;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Exceptions;
using JsonApiDotNetCore.Extensions;
using JsonApiDotNetCore.Internal;
using JsonApiDotNetCore.Models;
using JsonApiDotNetCore.Models.JsonApiDocuments;
using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace JsonApiDotNetCore.Controllers
{
    public abstract class BaseJsonApiController<T, TId> : JsonApiControllerMixin where T : class, IIdentifiable<TId>
    {
        private readonly IJsonApiOptions _jsonApiOptions;
        private readonly IGetAllService<T, TId> _getAll;
        private readonly IGetByIdService<T, TId> _getById;
        private readonly IGetRelationshipService<T, TId> _getRelationship;
        private readonly IGetRelationshipsService<T, TId> _getRelationships;
        private readonly ICreateService<T, TId> _create;
        private readonly IUpdateService<T, TId> _update;
        private readonly IUpdateRelationshipService<T, TId> _updateRelationships;
        private readonly IDeleteService<T, TId> _delete;
        private readonly ILogger<BaseJsonApiController<T, TId>> _logger;

        protected BaseJsonApiController(
            IJsonApiOptions jsonApiOptions,
            ILoggerFactory loggerFactory,
            IResourceService<T, TId> resourceService)
            : this(jsonApiOptions, loggerFactory, resourceService, resourceService, resourceService, resourceService,
                resourceService, resourceService, resourceService, resourceService)
        { }

        protected BaseJsonApiController(
            IJsonApiOptions jsonApiOptions,
            ILoggerFactory loggerFactory,
            IResourceQueryService<T, TId> queryService = null,
            IResourceCommandService<T, TId> commandService = null)
            : this(jsonApiOptions, loggerFactory, queryService, queryService, queryService, queryService, commandService,
                commandService, commandService, commandService)
        { }

        protected BaseJsonApiController(
            IJsonApiOptions jsonApiOptions,
            ILoggerFactory loggerFactory,
            IGetAllService<T, TId> getAll = null,
            IGetByIdService<T, TId> getById = null,
            IGetRelationshipService<T, TId> getRelationship = null,
            IGetRelationshipsService<T, TId> getRelationships = null,
            ICreateService<T, TId> create = null,
            IUpdateService<T, TId> update = null,
            IUpdateRelationshipService<T, TId> updateRelationships = null,
            IDeleteService<T, TId> delete = null)
        {
            _jsonApiOptions = jsonApiOptions;
            _logger = loggerFactory.CreateLogger<BaseJsonApiController<T, TId>>();
            _getAll = getAll;
            _getById = getById;
            _getRelationship = getRelationship;
            _getRelationships = getRelationships;
            _create = create;
            _update = update;
            _updateRelationships = updateRelationships;
            _delete = delete;

            _logger.LogTrace("Executing constructor.");
        }

        public virtual async Task<IActionResult> GetAsync()
        {
            if (_getAll == null) throw new RequestMethodNotAllowedException(HttpMethod.Get);
            var entities = await _getAll.GetAsync();
            return Ok(entities);
        }

        public virtual async Task<IActionResult> GetAsync(TId id)
        {
            if (_getById == null) throw new RequestMethodNotAllowedException(HttpMethod.Get);
            var entity = await _getById.GetAsync(id);
            if (entity == null)
            {
                return NotFound();
            }

            return Ok(entity);
        }

        public virtual async Task<IActionResult> GetRelationshipsAsync(TId id, string relationshipName)
        {
            if (_getRelationships == null) throw new RequestMethodNotAllowedException(HttpMethod.Get);
            var relationship = await _getRelationships.GetRelationshipsAsync(id, relationshipName);
            if (relationship == null)
            {
                return NotFound();
            }

            return Ok(relationship);
        }

        public virtual async Task<IActionResult> GetRelationshipAsync(TId id, string relationshipName)
        {
            if (_getRelationship == null) throw new RequestMethodNotAllowedException(HttpMethod.Get);
            var relationship = await _getRelationship.GetRelationshipAsync(id, relationshipName);
            return Ok(relationship);
        }

        public virtual async Task<IActionResult> PostAsync([FromBody] T entity)
        {
            if (_create == null)
                throw new RequestMethodNotAllowedException(HttpMethod.Post);

            if (entity == null)
                return UnprocessableEntity();

            if (!_jsonApiOptions.AllowClientGeneratedIds && !string.IsNullOrEmpty(entity.StringId))
                return Forbidden();

            if (_jsonApiOptions.ValidateModelState && !ModelState.IsValid)
                throw new InvalidModelStateException(ModelState, typeof(T), _jsonApiOptions);

            entity = await _create.CreateAsync(entity);

            return Created($"{HttpContext.Request.Path}/{entity.Id}", entity);
        }

        public virtual async Task<IActionResult> PatchAsync(TId id, [FromBody] T entity)
        {
            if (_update == null) throw new RequestMethodNotAllowedException(HttpMethod.Patch);
            if (entity == null)
                return UnprocessableEntity();

            if (_jsonApiOptions.ValidateModelState && !ModelState.IsValid)
                throw new InvalidModelStateException(ModelState, typeof(T), _jsonApiOptions);

            var updatedEntity = await _update.UpdateAsync(id, entity);

            if (updatedEntity == null)
            {
                return NotFound();
            }


            return Ok(updatedEntity);
        }

        public virtual async Task<IActionResult> PatchRelationshipsAsync(TId id, string relationshipName, [FromBody] object relationships)
        {
            if (_updateRelationships == null) throw new RequestMethodNotAllowedException(HttpMethod.Patch);
            await _updateRelationships.UpdateRelationshipsAsync(id, relationshipName, relationships);
            return Ok();
        }

        public virtual async Task<IActionResult> DeleteAsync(TId id)
        {
            if (_delete == null) throw new RequestMethodNotAllowedException(HttpMethod.Delete);
            var wasDeleted = await _delete.DeleteAsync(id);
            if (!wasDeleted)
                return NotFound();
            return NoContent();
        }
    }

    public abstract class BaseJsonApiController<T> : BaseJsonApiController<T, int> where T : class, IIdentifiable<int>
    {
        protected BaseJsonApiController(
            IJsonApiOptions jsonApiOptions,
            ILoggerFactory loggerFactory,
            IResourceService<T, int> resourceService)
            : base(jsonApiOptions, loggerFactory, resourceService, resourceService)
        { }

        protected BaseJsonApiController(
            IJsonApiOptions jsonApiOptions,
            ILoggerFactory loggerFactory,
            IResourceQueryService<T, int> queryService = null,
            IResourceCommandService<T, int> commandService = null)
            : base(jsonApiOptions, loggerFactory, queryService, commandService)
        { }

        protected BaseJsonApiController(
            IJsonApiOptions jsonApiOptions,
            ILoggerFactory loggerFactory,
            IGetAllService<T, int> getAll = null,
            IGetByIdService<T, int> getById = null,
            IGetRelationshipService<T, int> getRelationship = null,
            IGetRelationshipsService<T, int> getRelationships = null,
            ICreateService<T, int> create = null,
            IUpdateService<T, int> update = null,
            IUpdateRelationshipService<T, int> updateRelationships = null,
            IDeleteService<T, int> delete = null)
            : base(jsonApiOptions, loggerFactory, getAll, getById, getRelationship, getRelationships, create, update,
                updateRelationships, delete)
        { }
    }
}
