using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JsonApiDotNetCore.Extensions;
using JsonApiDotNetCore.Internal;
using JsonApiDotNetCore.Internal.Generics;
using JsonApiDotNetCore.Internal.Query;
using JsonApiDotNetCore.Models;
using JsonApiDotNetCore.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JsonApiDotNetCore.Data
{
    /// <inheritdoc />
    public class DefaultEntityRepository<TEntity>
        : DefaultEntityRepository<TEntity, int>,
        IEntityRepository<TEntity>
        where TEntity : class, IIdentifiable<int>
    {
        public DefaultEntityRepository(
            ILoggerFactory loggerFactory,
            IJsonApiContext jsonApiContext,
            IDbContextResolver contextResolver)
        : base(loggerFactory, jsonApiContext, contextResolver)
        { }
    }

    /// <summary>
    /// Provides a default repository implementation and is responsible for
    /// abstracting any EF Core APIs away from the service layer.
    /// </summary>
    public class DefaultEntityRepository<TEntity, TId>
        : IEntityRepository<TEntity, TId>
        where TEntity : class, IIdentifiable<TId>
    {
        private readonly DbContext _context;
        private readonly DbSet<TEntity> _dbSet;
        private readonly ILogger _logger;
        private readonly IJsonApiContext _jsonApiContext;
        private readonly IGenericProcessorFactory _genericProcessorFactory;

        public DefaultEntityRepository(
            ILoggerFactory loggerFactory,
            IJsonApiContext jsonApiContext,
            IDbContextResolver contextResolver)
        {
            _context = contextResolver.GetContext();
            _dbSet = contextResolver.GetDbSet<TEntity>();
            _jsonApiContext = jsonApiContext;
            _logger = loggerFactory.CreateLogger<DefaultEntityRepository<TEntity, TId>>();
            _genericProcessorFactory = _jsonApiContext.GenericProcessorFactory;
        }

        /// <inheritdoc />
        public virtual IQueryable<TEntity> Get()
        {
            if (_jsonApiContext.QuerySet?.Fields != null && _jsonApiContext.QuerySet.Fields.Count > 0)
                return _dbSet.Select(_jsonApiContext.QuerySet?.Fields);

            return _dbSet;
        }

        /// <inheritdoc />
        public virtual IQueryable<TEntity> Filter(IQueryable<TEntity> entities, FilterQuery filterQuery)
        {
            return entities.Filter(_jsonApiContext, filterQuery);
        }

        /// <inheritdoc />
        public virtual IQueryable<TEntity> Sort(IQueryable<TEntity> entities, List<SortQuery> sortQueries)
        {
            return entities.Sort(sortQueries);
        }

        /// <inheritdoc />
        public virtual async Task<TEntity> GetAsync(TId id)
        {
            return await Get().SingleOrDefaultAsync(e => e.Id.Equals(id));
        }

        /// <inheritdoc />
        public virtual async Task<TEntity> GetAndIncludeAsync(TId id, string relationshipName)
        {
            _logger.LogDebug($"[JADN] GetAndIncludeAsync({id}, {relationshipName})");

            var includedSet = await IncludeAsync(Get(), relationshipName);
            var result = await includedSet.SingleOrDefaultAsync(e => e.Id.Equals(id));

            return result;
        }

        /// <inheritdoc />
        public virtual async Task<TEntity> CreateAsync(TEntity entity)
        {
            AttachRelationships();
            _dbSet.Add(entity);

            await _context.SaveChangesAsync();

            return entity;
        }

        protected virtual void AttachRelationships()
        {
            AttachHasManyPointers();
            AttachHasOnePointers();
        }

        /// <summary>
        /// This is used to allow creation of HasMany relationships when the
        /// dependent side of the relationship already exists.
        /// </summary>
        private void AttachHasManyPointers()
        {
            var relationships = _jsonApiContext.HasManyRelationshipPointers.Get();
            foreach (var relationship in relationships)
            {
                foreach (var pointer in relationship.Value)
                {
                    _context.Entry(pointer).State = EntityState.Unchanged;
                }
            }
        }

        /// <summary>
        /// This is used to allow creation of HasOne relationships when the
        /// independent side of the relationship already exists.
        /// </summary>
        private void AttachHasOnePointers()
        {
            var relationships = _jsonApiContext.HasOneRelationshipPointers.Get();
            foreach (var relationship in relationships)
                if (_context.Entry(relationship.Value).State == EntityState.Detached && _context.EntityIsTracked(relationship.Value) == false)
                    _context.Entry(relationship.Value).State = EntityState.Unchanged;
        }

        /// <inheritdoc />
        public virtual async Task<TEntity> UpdateAsync(TId id, TEntity entity)
        {
            var oldEntity = await GetAsync(id);

            if (oldEntity == null)
                return null;

            foreach (var attr in _jsonApiContext.AttributesToUpdate)
                attr.Key.SetValue(oldEntity, attr.Value);

            foreach (var relationship in _jsonApiContext.RelationshipsToUpdate)
                relationship.Key.SetValue(oldEntity, relationship.Value);

            await _context.SaveChangesAsync();

            return oldEntity;
        }

        /// <inheritdoc />
        public async Task UpdateRelationshipsAsync(object parent, RelationshipAttribute relationship, IEnumerable<string> relationshipIds)
        {
            var genericProcessor = _genericProcessorFactory.GetProcessor<IGenericProcessor>(typeof(GenericProcessor<>), relationship.Type);
            await genericProcessor.UpdateRelationshipsAsync(parent, relationship, relationshipIds);
        }

        /// <inheritdoc />
        public virtual async Task<bool> DeleteAsync(TId id)
        {
            var entity = await GetAsync(id);

            if (entity == null)
                return false;

            _dbSet.Remove(entity);

            await _context.SaveChangesAsync();

            return true;
        }

        /// <inheritdoc />
        [Obsolete("Use IncludeAsync")]
        public virtual IQueryable<TEntity> Include(IQueryable<TEntity> entities, string relationshipName)
        {
            var entity = _jsonApiContext.RequestEntity;
            var relationship = entity.Relationships.FirstOrDefault(r => r.PublicRelationshipName == relationshipName);
            if (relationship == null)
            {
                throw new JsonApiException(400, $"Invalid relationship {relationshipName} on {entity.EntityName}",
                    $"{entity.EntityName} does not have a relationship named {relationshipName}");
            }

            if (!relationship.CanInclude)
            {
                throw new JsonApiException(400, $"Including the relationship {relationshipName} on {entity.EntityName} is not allowed");
            }

            return entities.Include(relationship.InternalRelationshipName);
        }

        /// <inheritdoc />
        public virtual async Task<IQueryable<TEntity>> IncludeAsync(IQueryable<TEntity> entities, string relationshipName)
        {
            var entity = _jsonApiContext.RequestEntity;
            var relationship = entity.Relationships.FirstOrDefault(r => r.PublicRelationshipName == relationshipName);
            if (relationship == null)
            {
                throw new JsonApiException(400, $"Invalid relationship {relationshipName} on {entity.EntityName}",
                    $"{entity.EntityName} does not have a relationship named {relationshipName}");
            }

            if (!relationship.CanInclude)
            {
                throw new JsonApiException(400, $"Including the relationship {relationshipName} on {entity.EntityName} is not allowed");
            }

            await ReloadPointerAsync(relationship);

            return entities.Include(relationship.InternalRelationshipName);
        }

        /// <summary>
        /// Ensure relationships on the provided entity have been fully loaded from the database.
        /// </summary>
        /// <remarks>
        /// The only known case when this should be called is when a POST request is
        /// sent with an ?include query.
        /// 
        /// See https://github.com/json-api-dotnet/JsonApiDotNetCore/issues/343
        /// </remarks>
        private async Task ReloadPointerAsync(RelationshipAttribute relationshipAttr)
        {
            if (relationshipAttr.IsHasOne && _jsonApiContext.HasOneRelationshipPointers.Get().TryGetValue(relationshipAttr, out var pointer))
            {
                await _context.Entry(pointer).ReloadAsync();
            }

            if (relationshipAttr.IsHasMany && _jsonApiContext.HasManyRelationshipPointers.Get().TryGetValue(relationshipAttr, out var pointers))
            {
                foreach (var hasManyPointer in pointers)
                {
                    await _context.Entry(hasManyPointer).ReloadAsync();
                }
            }
        }

        /// <inheritdoc />
        public virtual async Task<IEnumerable<TEntity>> PageAsync(IQueryable<TEntity> entities, int pageSize, int pageNumber)
        {
            if (pageNumber >= 0)
            {
                return await entities.PageForward(pageSize, pageNumber).ToListAsync();
            }

            // since EntityFramework does not support IQueryable.Reverse(), we need to know the number of queried entities
            int numberOfEntities = await this.CountAsync(entities);

            // may be negative
            int virtualFirstIndex = numberOfEntities - pageSize * Math.Abs(pageNumber);
            int numberOfElementsInPage = Math.Min(pageSize, virtualFirstIndex + pageSize);

            return await entities
                    .Skip(virtualFirstIndex)
                    .Take(numberOfElementsInPage)
                    .ToListAsync();
        }

        /// <inheritdoc />
        public async Task<int> CountAsync(IQueryable<TEntity> entities)
        {
            return (entities is IAsyncEnumerable<TEntity>)
                 ? await entities.CountAsync()
                 : entities.Count();
        }

        /// <inheritdoc />
        public async Task<TEntity> FirstOrDefaultAsync(IQueryable<TEntity> entities)
        {
            return (entities is IAsyncEnumerable<TEntity>)
               ? await entities.FirstOrDefaultAsync()
               : entities.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<TEntity>> ToListAsync(IQueryable<TEntity> entities)
        {
            return (entities is IAsyncEnumerable<TEntity>)
                ? await entities.ToListAsync()
                : entities.ToList();
        }
    }
}
