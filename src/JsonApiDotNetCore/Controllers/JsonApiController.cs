using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Extensions;
using JsonApiDotNetCore.Internal.Query;
using JsonApiDotNetCore.Models;
using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace JsonApiDotNetCore.Controllers
{
    public class JsonApiController<T> 
    : JsonApiController<T, int> where T : class, IIdentifiable<int>
    {
        public JsonApiController(
            IJsonApiContext jsonApiContext,
            IEntityRepository<T, int> entityRepository,
            ILoggerFactory loggerFactory)
            : base(jsonApiContext, entityRepository, loggerFactory)
        { }
    }

    public class JsonApiController<T, TId> 
    : Controller where T : class, IIdentifiable<TId>
    {
        private readonly IEntityRepository<T, TId> _entities;
        private readonly IJsonApiContext _jsonApiContext;
        private readonly ILogger _logger;

        public JsonApiController(
            IJsonApiContext jsonApiContext,
            IEntityRepository<T, TId> entityRepository,
            ILoggerFactory loggerFactory)
        {
            _jsonApiContext = jsonApiContext.ApplyContext<T>();
            _entities = entityRepository;

            _logger = loggerFactory.CreateLogger<JsonApiController<T, TId>>();
            _logger.LogTrace($@"JsonApiController activated with ContextGraph: 
                {JsonConvert.SerializeObject(jsonApiContext.ContextGraph)}");
        }

        public JsonApiController(
            IJsonApiContext jsonApiContext,
            IEntityRepository<T, TId> entityRepository)
        {
            _jsonApiContext = jsonApiContext;
            _entities = entityRepository;
        }

        [HttpGet]
        public virtual IActionResult Get()
        {
            var entities = _entities.Get();

            entities = ApplySortAndFilterQuery(entities);

            if(_jsonApiContext.QuerySet != null)
                entities = IncludeRelationships(entities, _jsonApiContext.QuerySet.IncludedRelationships);

            return Ok(entities);
        }

        [HttpGet("{id}")]
        public virtual async Task<IActionResult> GetAsync(TId id)
        {
            T entity;
            if(_jsonApiContext.QuerySet?.IncludedRelationships != null)
                entity = await _getWithRelationshipsAsync(id);
            else
                entity = await _entities.GetAsync(id);

            if (entity == null)
                return NotFound();

            return Ok(entity);
        }

        private async Task<T> _getWithRelationshipsAsync(TId id)
        {
            var query = _entities.Get();
            _jsonApiContext.QuerySet.IncludedRelationships.ForEach(r =>
            {
                query = _entities.Include(query, r.ToProperCase());
            });
            return await query.FirstOrDefaultAsync(e => e.Id.Equals(id));
        }

        [HttpGet("{id}/relationships/{relationshipName}")]
        public virtual async Task<IActionResult> GetRelationshipsAsync(TId id, string relationshipName)
        {
            _jsonApiContext.IsRelationshipData = true;

            return await GetRelationshipAsync(id, relationshipName);
        }

        [HttpGet("{id}/{relationshipName}")]
        public virtual async Task<IActionResult> GetRelationshipAsync(TId id, string relationshipName)
        {
            relationshipName = _jsonApiContext.ContextGraph
                .GetRelationshipName<T>(relationshipName);

            if (relationshipName == null)
                return NotFound();

            var entity = await _entities.GetAndIncludeAsync(id, relationshipName);

            if (entity == null)
                return NotFound();

            var relationship = _jsonApiContext.ContextGraph
                .GetRelationship<T>(entity, relationshipName);

            if (relationship == null)
                return NotFound();

            return Ok(relationship);
        }

        [HttpPost]
        public virtual async Task<IActionResult> PostAsync([FromBody] T entity)
        {
            if (entity == null)
                return BadRequest();

            await _entities.CreateAsync(entity);

            return Created(HttpContext.Request.Path, entity);
        }

        [HttpPatch("{id}")]
        public virtual async Task<IActionResult> PatchAsync(TId id, [FromBody] T entity)
        {
            if (entity == null)
                return BadRequest();

            var updatedEntity = await _entities.UpdateAsync(id, entity);

            return Ok(updatedEntity);
        }

        // [HttpPatch("{id}/{relationship}")]
        // public virtual IActionResult PatchRelationship(int id, string relation) 
        // {
        //     return Ok("Patch Id/relationship");
        // }

        [HttpDelete("{id}")]
        public virtual async Task<IActionResult> DeleteAsync(TId id)
        {
            var wasDeleted = await _entities.DeleteAsync(id);

            if (!wasDeleted)
                return NotFound();

            return Ok();
        }

        // [HttpDelete("{id}/{relationship}")]
        // public virtual IActionResult Delete(int id, string relation) 
        // {
        //     return Ok("Delete Id/relationship");
        // }

        private IQueryable<T> ApplySortAndFilterQuery(IQueryable<T> entities)
        {
            var query = _jsonApiContext.QuerySet;

            if(_jsonApiContext.QuerySet == null)
                return entities;

            entities = _entities.Filter(entities, query.Filter);

            entities = _entities.Sort(entities, query.SortParameters);

            return entities;
        }

        private IQueryable<T> IncludeRelationships(IQueryable<T> entities, List<string> relationships)
        {
            _jsonApiContext.IncludedRelationships = relationships;

            foreach(var r in relationships)
                entities = _entities.Include(entities, r.ToProperCase());

            return entities;
        }
    }
}
