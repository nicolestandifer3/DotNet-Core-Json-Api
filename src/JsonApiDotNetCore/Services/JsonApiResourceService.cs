using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Errors;
using JsonApiDotNetCore.Hooks.Internal;
using JsonApiDotNetCore.Hooks.Internal.Execution;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Queries.Expressions;
using JsonApiDotNetCore.Repositories;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Resources.Annotations;
using Microsoft.Extensions.Logging;

namespace JsonApiDotNetCore.Services
{
    /// <inheritdoc />
    public class JsonApiResourceService<TResource, TId> :
        IResourceService<TResource, TId>
        where TResource : class, IIdentifiable<TId>
    {
        private readonly IResourceRepository<TResource, TId> _repository;
        private readonly IQueryLayerComposer _queryLayerComposer;
        private readonly IPaginationContext _paginationContext;
        private readonly IJsonApiOptions _options;
        private readonly TraceLogWriter<JsonApiResourceService<TResource, TId>> _traceWriter;
        private readonly IJsonApiRequest _request;
        private readonly IResourceChangeTracker<TResource> _resourceChangeTracker;
        private readonly IResourceFactory _resourceFactory;
        private readonly IResourceHookExecutor _hookExecutor;

        public JsonApiResourceService(
            IResourceRepository<TResource, TId> repository,
            IQueryLayerComposer queryLayerComposer,
            IPaginationContext paginationContext,
            IJsonApiOptions options,
            ILoggerFactory loggerFactory,
            IJsonApiRequest request,
            IResourceChangeTracker<TResource> resourceChangeTracker,
            IResourceFactory resourceFactory,
            IResourceHookExecutor hookExecutor = null)
        {
            if (loggerFactory == null) throw new ArgumentNullException(nameof(loggerFactory));

            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _queryLayerComposer = queryLayerComposer ?? throw new ArgumentNullException(nameof(queryLayerComposer));
            _paginationContext = paginationContext ?? throw new ArgumentNullException(nameof(paginationContext));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _traceWriter = new TraceLogWriter<JsonApiResourceService<TResource, TId>>(loggerFactory);
            _request = request ?? throw new ArgumentNullException(nameof(request));
            _resourceChangeTracker = resourceChangeTracker ?? throw new ArgumentNullException(nameof(resourceChangeTracker));
            _resourceFactory = resourceFactory ?? throw new ArgumentNullException(nameof(resourceFactory));
            _hookExecutor = hookExecutor;
        }

        /// <inheritdoc />
        public virtual async Task<TResource> CreateAsync(TResource resource)
        {
            _traceWriter.LogMethodStart(new {resource});
            if (resource == null) throw new ArgumentNullException(nameof(resource));

            if (_hookExecutor != null)
            {
                resource = _hookExecutor.BeforeCreate(AsList(resource), ResourcePipeline.Post).Single();
            }
            
            await _repository.CreateAsync(resource);

            resource = await GetPrimaryResourceById(resource.Id, true);

            if (_hookExecutor != null)
            {
                _hookExecutor.AfterCreate(AsList(resource), ResourcePipeline.Post);
                resource = _hookExecutor.OnReturn(AsList(resource), ResourcePipeline.Post).Single();
            }

            return resource;
        }

        /// <inheritdoc />
        public virtual async Task DeleteAsync(TId id)
        {
            _traceWriter.LogMethodStart(new {id});

            if (_hookExecutor != null)
            {
                var resource = _resourceFactory.CreateInstance<TResource>();
                resource.Id = id;

                _hookExecutor.BeforeDelete(AsList(resource), ResourcePipeline.Delete);
            }

            var succeeded = await _repository.DeleteAsync(id);

            if (_hookExecutor != null)
            {
                var resource = _resourceFactory.CreateInstance<TResource>();
                resource.Id = id;

                _hookExecutor.AfterDelete(AsList(resource), ResourcePipeline.Delete, succeeded);
            }

            if (!succeeded)
            {
                AssertPrimaryResourceExists(null);
            }
        }

        /// <inheritdoc />
        public virtual async Task<IReadOnlyCollection<TResource>> GetAsync()
        {
            _traceWriter.LogMethodStart();

            _hookExecutor?.BeforeRead<TResource>(ResourcePipeline.Get);

            if (_options.IncludeTotalResourceCount)
            {
                var topFilter = _queryLayerComposer.GetTopFilter();
                _paginationContext.TotalResourceCount = await _repository.CountAsync(topFilter);
            }

            var queryLayer = _queryLayerComposer.Compose(_request.PrimaryResource);
            var resources = await _repository.GetAsync(queryLayer);

            if (_hookExecutor != null)
            {
                _hookExecutor.AfterRead(resources, ResourcePipeline.Get);
                return _hookExecutor.OnReturn(resources, ResourcePipeline.Get).ToArray();
            }

            return resources;
        }

        /// <inheritdoc />
        public virtual async Task<TResource> GetAsync(TId id)
        {
            _traceWriter.LogMethodStart(new {id});

            _hookExecutor?.BeforeRead<TResource>(ResourcePipeline.GetSingle, id.ToString());

            var primaryResource = await GetPrimaryResourceById(id, true);

            if (_hookExecutor != null)
            {
                _hookExecutor.AfterRead(AsList(primaryResource), ResourcePipeline.GetSingle);
                return _hookExecutor.OnReturn(AsList(primaryResource), ResourcePipeline.GetSingle).Single();
            }

            return primaryResource;
        }

        private async Task<TResource> GetPrimaryResourceById(TId id, bool allowTopSparseFieldSet)
        {
            var primaryLayer = _queryLayerComposer.Compose(_request.PrimaryResource);
            primaryLayer.Sort = null;
            primaryLayer.Pagination = null;
            primaryLayer.Filter = CreateFilterById(id);

            if (!allowTopSparseFieldSet && primaryLayer.Projection != null)
            {
                // Discard any ?fields= or attribute exclusions from ResourceDefinition, because we need the full record.

                while (primaryLayer.Projection.Any(p => p.Key is AttrAttribute))
                {
                    primaryLayer.Projection.Remove(primaryLayer.Projection.First(p => p.Key is AttrAttribute));
                }
            }

            var primaryResources = await _repository.GetAsync(primaryLayer);

            var primaryResource = primaryResources.SingleOrDefault();
            AssertPrimaryResourceExists(primaryResource);

            return primaryResource;
        }

        private FilterExpression CreateFilterById(TId id)
        {
            var primaryIdAttribute = _request.PrimaryResource.Attributes.Single(a => a.Property.Name == nameof(Identifiable.Id));

            return new ComparisonExpression(ComparisonOperator.Equals,
                new ResourceFieldChainExpression(primaryIdAttribute), new LiteralConstantExpression(id.ToString()));
        }

        /// <inheritdoc />
        // triggered by GET /articles/1/relationships/{relationshipName}
        public virtual async Task<TResource> GetRelationshipAsync(TId id, string relationshipName)
        {
            _traceWriter.LogMethodStart(new {id, relationshipName});
            if (relationshipName == null) throw new ArgumentNullException(nameof(relationshipName));

            AssertRelationshipExists(relationshipName);

            _hookExecutor?.BeforeRead<TResource>(ResourcePipeline.GetRelationship, id.ToString());

            var secondaryLayer = _queryLayerComposer.Compose(_request.SecondaryResource);
            secondaryLayer.Projection = _queryLayerComposer.GetSecondaryProjectionForRelationshipEndpoint(_request.SecondaryResource);
            secondaryLayer.Include = null;

            var primaryLayer = _queryLayerComposer.WrapLayerForSecondaryEndpoint(secondaryLayer, _request.PrimaryResource, id, _request.Relationship);

            var primaryResources = await _repository.GetAsync(primaryLayer);

            var primaryResource = primaryResources.SingleOrDefault();
            AssertPrimaryResourceExists(primaryResource);

            if (_hookExecutor != null)
            {
                _hookExecutor.AfterRead(AsList(primaryResource), ResourcePipeline.GetRelationship);
                primaryResource = _hookExecutor.OnReturn(AsList(primaryResource), ResourcePipeline.GetRelationship).Single();
            }

            return primaryResource;
        }

        /// <inheritdoc />
        // triggered by GET /articles/1/{relationshipName}
        public virtual async Task<object> GetSecondaryAsync(TId id, string relationshipName)
        {
            _traceWriter.LogMethodStart(new {id, relationshipName});
            if (relationshipName == null) throw new ArgumentNullException(nameof(relationshipName));

            AssertRelationshipExists(relationshipName);

            _hookExecutor?.BeforeRead<TResource>(ResourcePipeline.GetRelationship, id.ToString());

            var secondaryLayer = _queryLayerComposer.Compose(_request.SecondaryResource);
            var primaryLayer = _queryLayerComposer.WrapLayerForSecondaryEndpoint(secondaryLayer, _request.PrimaryResource, id, _request.Relationship);

            var primaryResources = await _repository.GetAsync(primaryLayer);
            
            var primaryResource = primaryResources.SingleOrDefault();
            AssertPrimaryResourceExists(primaryResource);

            if (_hookExecutor != null)
            {   
                _hookExecutor.AfterRead(AsList(primaryResource), ResourcePipeline.GetRelationship);
                primaryResource = _hookExecutor.OnReturn(AsList(primaryResource), ResourcePipeline.GetRelationship).Single();
            }

            return _request.Relationship.GetValue(primaryResource);
        }

        /// <inheritdoc />
        public virtual async Task<TResource> UpdateAsync(TId id, TResource requestResource)
        {
            _traceWriter.LogMethodStart(new {id, requestResource});
            if (requestResource == null) throw new ArgumentNullException(nameof(requestResource));

            TResource databaseResource = await GetPrimaryResourceById(id, false);

            _resourceChangeTracker.SetInitiallyStoredAttributeValues(databaseResource);
            _resourceChangeTracker.SetRequestedAttributeValues(requestResource);

            if (_hookExecutor != null)
            {
                requestResource = _hookExecutor.BeforeUpdate(AsList(requestResource), ResourcePipeline.Patch).Single();
            }

            await _repository.UpdateAsync(requestResource, databaseResource);

            if (_hookExecutor != null)
            {
                _hookExecutor.AfterUpdate(AsList(databaseResource), ResourcePipeline.Patch);
                _hookExecutor.OnReturn(AsList(databaseResource), ResourcePipeline.Patch);
            }

            _repository.FlushFromCache(databaseResource);
            TResource afterResource = await GetPrimaryResourceById(id, false);
            _resourceChangeTracker.SetFinallyStoredAttributeValues(afterResource);

            bool hasImplicitChanges = _resourceChangeTracker.HasImplicitChanges();
            return hasImplicitChanges ? afterResource : null;
        }

        /// <inheritdoc />
        // triggered by PATCH /articles/1/relationships/{relationshipName}
        public virtual async Task UpdateRelationshipAsync(TId id, string relationshipName, object relationships)
        {
            _traceWriter.LogMethodStart(new {id, relationshipName, related = relationships});
            if (relationshipName == null) throw new ArgumentNullException(nameof(relationshipName));

            AssertRelationshipExists(relationshipName);

            var secondaryLayer = _queryLayerComposer.Compose(_request.SecondaryResource);
            var primaryLayer = _queryLayerComposer.WrapLayerForSecondaryEndpoint(secondaryLayer, _request.PrimaryResource, id, _request.Relationship);
            primaryLayer.Projection = null;

            var primaryResources = await _repository.GetAsync(primaryLayer);

            var primaryResource = primaryResources.SingleOrDefault();
            AssertPrimaryResourceExists(primaryResource);

            if (_hookExecutor != null)
            {
                primaryResource = _hookExecutor.BeforeUpdate(AsList(primaryResource), ResourcePipeline.PatchRelationship).Single();
            }

            string[] relationshipIds = null;
            if (relationships != null)
            {
                relationshipIds = _request.Relationship is HasOneAttribute
                    ? new[] {((IIdentifiable) relationships).StringId}
                    : ((IEnumerable<IIdentifiable>) relationships).Select(e => e.StringId).ToArray();
            }

            await _repository.UpdateRelationshipsAsync(primaryResource, _request.Relationship, relationshipIds ?? Array.Empty<string>());

            if (_hookExecutor != null && primaryResource != null)
            {
                _hookExecutor.AfterUpdate(AsList(primaryResource), ResourcePipeline.PatchRelationship);
            }
        }

        private void AssertPrimaryResourceExists(TResource resource)
        {
            if (resource == null)
            {
                throw new ResourceNotFoundException(_request.PrimaryId, _request.PrimaryResource.ResourceName);
            }
        }

        private void AssertRelationshipExists(string relationshipName)
        {
            var relationship = _request.Relationship;
            if (relationship == null)
            {
                throw new RelationshipNotFoundException(relationshipName, _request.PrimaryResource.ResourceName);
            }
        }

        private static List<TResource> AsList(TResource resource)
        {
            return new List<TResource> { resource };
        }
    }

    /// <summary>
    /// Represents the foundational Resource Service layer in the JsonApiDotNetCore architecture that uses a Resource Repository for data access.
    /// </summary>
    /// <typeparam name="TResource">The resource type.</typeparam>
    public class JsonApiResourceService<TResource> : JsonApiResourceService<TResource, int>,
        IResourceService<TResource>
        where TResource : class, IIdentifiable<int>
    {
        public JsonApiResourceService(
            IResourceRepository<TResource, int> repository,
            IQueryLayerComposer queryLayerComposer,
            IPaginationContext paginationContext,
            IJsonApiOptions options,
            ILoggerFactory loggerFactory,
            IJsonApiRequest request,
            IResourceChangeTracker<TResource> resourceChangeTracker,
            IResourceFactory resourceFactory,
            IResourceHookExecutor hookExecutor = null)
            : base(repository, queryLayerComposer, paginationContext, options, loggerFactory, request,
                resourceChangeTracker, resourceFactory, hookExecutor)
        { }
    }
}
