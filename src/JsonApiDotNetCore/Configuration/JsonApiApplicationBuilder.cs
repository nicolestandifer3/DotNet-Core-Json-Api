using System;
using JsonApiDotNetCore.Hooks.Internal;
using JsonApiDotNetCore.Hooks.Internal.Discovery;
using JsonApiDotNetCore.Hooks.Internal.Execution;
using JsonApiDotNetCore.Hooks.Internal.Traversal;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Queries.Internal;
using JsonApiDotNetCore.QueryStrings;
using JsonApiDotNetCore.QueryStrings.Internal;
using JsonApiDotNetCore.Repositories;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Serialization;
using JsonApiDotNetCore.Serialization.Building;
using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace JsonApiDotNetCore.Configuration
{
    /// <summary>
    /// A utility class that builds a JsonApi application. It registers all required services
    /// and allows the user to override parts of the startup configuration.
    /// </summary>
    internal sealed class JsonApiApplicationBuilder : IJsonApiApplicationBuilder, IDisposable
    {
        private readonly JsonApiOptions _options = new JsonApiOptions();
        private readonly IServiceCollection _services;
        private readonly IMvcCoreBuilder _mvcBuilder;
        private readonly ResourceGraphBuilder _resourceGraphBuilder;
        private readonly ServiceDiscoveryFacade _serviceDiscoveryFacade;
        private readonly ServiceProvider _intermediateProvider;
        
        public Action<MvcOptions> ConfigureMvcOptions { get; set; }

        public JsonApiApplicationBuilder(IServiceCollection services, IMvcCoreBuilder mvcBuilder)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _mvcBuilder = mvcBuilder ?? throw new ArgumentNullException(nameof(mvcBuilder));
            
            _intermediateProvider = services.BuildServiceProvider();
            var loggerFactory = _intermediateProvider.GetService<ILoggerFactory>();
            
            _resourceGraphBuilder = new ResourceGraphBuilder(_options, loggerFactory);
            _serviceDiscoveryFacade = new ServiceDiscoveryFacade(_services, _resourceGraphBuilder, loggerFactory);
        }
        
        /// <summary>
        /// Executes the action provided by the user to configure <see cref="JsonApiOptions"/>.
        /// </summary>
        public void ConfigureJsonApiOptions(Action<JsonApiOptions> configureOptions)
        {
            configureOptions?.Invoke(_options);
        }
        
        /// <summary>
        /// Executes the action provided by the user to configure <see cref="ServiceDiscoveryFacade"/>.
        /// </summary>
        public void ConfigureAutoDiscovery(Action<ServiceDiscoveryFacade> configureAutoDiscovery)
        {
            configureAutoDiscovery?.Invoke(_serviceDiscoveryFacade);
        }

        /// <summary>
        /// Configures and builds the resource graph with resources from the provided sources and adds it to the DI container. 
        /// </summary>
        public void AddResourceGraph(Type dbContextType, Action<ResourceGraphBuilder> configureResourceGraph)
        {
            _serviceDiscoveryFacade.DiscoverResources();
            
            if (dbContextType != null)
            {
                AddResourcesFromDbContext((DbContext)_intermediateProvider.GetService(dbContextType), _resourceGraphBuilder);
            }
            
            configureResourceGraph?.Invoke(_resourceGraphBuilder);

            var resourceGraph = _resourceGraphBuilder.Build();
            _services.AddSingleton(resourceGraph);
        }

        /// <summary>
        /// Configures built-in ASP.NET Core MVC components. Most of this configuration can be adjusted for the developers' need.
        /// </summary>
        public void ConfigureMvc()
        {
            _mvcBuilder.AddMvcOptions(options =>
            {
                options.EnableEndpointRouting = true;
                options.Filters.AddService<IAsyncJsonApiExceptionFilter>();
                options.Filters.AddService<IAsyncQueryStringActionFilter>();
                options.Filters.AddService<IAsyncConvertEmptyActionResultFilter>();
                ConfigureMvcOptions?.Invoke(options);
            });

            if (_options.ValidateModelState)
            {
                _mvcBuilder.AddDataAnnotations();
            }
        }

        /// <summary>
        /// Discovers DI registrable services in the assemblies marked for discovery.
        /// </summary>
        public void DiscoverInjectables()
        {
            _serviceDiscoveryFacade.DiscoverInjectables();
        }

        /// <summary>
        /// Registers the remaining internals.
        /// </summary>
        public void ConfigureServices(Type dbContextType)
        {
            if (dbContextType != null)
            {
                var contextResolverType = typeof(DbContextResolver<>).MakeGenericType(dbContextType);
                _services.TryAddScoped(typeof(IDbContextResolver), contextResolverType);
            }
            else
            {
                _services.AddScoped<DbContext>();
                _services.AddSingleton(new DbContextOptionsBuilder().Options);
            }

            AddRepositoryLayer();
            AddServiceLayer();
            AddMiddlewareLayer();

            _services.AddSingleton<IResourceContextProvider>(sp => sp.GetRequiredService<IResourceGraph>());
            
            _services.AddScoped<IGenericServiceFactory, GenericServiceFactory>();
            _services.AddScoped(typeof(RepositoryRelationshipUpdateHelper<>));
            _services.AddScoped<IResourceDefinitionProvider, ResourceDefinitionProvider>();
            _services.AddScoped(typeof(IResourceChangeTracker<>), typeof(ResourceChangeTracker<>));
            _services.AddScoped<IResourceFactory, ResourceFactory>();
            _services.AddScoped<IPaginationContext, PaginationContext>();
            _services.AddScoped<IQueryLayerComposer, QueryLayerComposer>();

            AddServerSerialization();
            AddQueryStringParameterServices();
            
            if (_options.EnableResourceHooks)
            {
                AddResourceHooks();
            }

            _services.TryAddScoped<IInverseRelationships, InverseRelationships>();
        }

        private void AddMiddlewareLayer()
        {
            _services.AddSingleton<IJsonApiOptions>(_options);
            _services.AddSingleton<IJsonApiApplicationBuilder>(this);
            _services.TryAddSingleton<IExceptionHandler, ExceptionHandler>();
            _services.TryAddScoped<IAsyncJsonApiExceptionFilter, AsyncJsonApiExceptionFilter>();
            _services.TryAddScoped<IAsyncQueryStringActionFilter, AsyncQueryStringActionFilter>();
            _services.TryAddScoped<IAsyncConvertEmptyActionResultFilter, AsyncConvertEmptyActionResultFilter>();
            _services.TryAddSingleton<IJsonApiInputFormatter, JsonApiInputFormatter>();
            _services.TryAddSingleton<IJsonApiOutputFormatter, JsonApiOutputFormatter>();
            _services.TryAddSingleton<IJsonApiRoutingConvention, JsonApiRoutingConvention>();
            _services.AddSingleton<IControllerResourceMapping>(sp => sp.GetService<IJsonApiRoutingConvention>());
            _services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            _services.AddScoped<IRequestScopedServiceProvider, RequestScopedServiceProvider>();
            _services.AddScoped<IJsonApiRequest, JsonApiRequest>();
            _services.AddScoped<IJsonApiWriter, JsonApiWriter>();
            _services.AddScoped<IJsonApiReader, JsonApiReader>();
            _services.AddScoped<ITargetedFields, TargetedFields>();
            _services.AddScoped<IFieldsToSerialize, FieldsToSerialize>();
        }

        private void AddRepositoryLayer()
        {
            _services.AddScoped(typeof(IResourceRepository<>), typeof(EntityFrameworkCoreRepository<>));
            _services.AddScoped(typeof(IResourceRepository<,>), typeof(EntityFrameworkCoreRepository<,>));

            _services.AddScoped(typeof(IResourceReadRepository<,>), typeof(EntityFrameworkCoreRepository<,>));
            _services.AddScoped(typeof(IResourceWriteRepository<,>), typeof(EntityFrameworkCoreRepository<,>));
        }

        private void AddServiceLayer()
        {
            _services.AddScoped(typeof(ICreateService<>), typeof(JsonApiResourceService<>));
            _services.AddScoped(typeof(ICreateService<,>), typeof(JsonApiResourceService<,>));

            _services.AddScoped(typeof(IGetAllService<>), typeof(JsonApiResourceService<>));
            _services.AddScoped(typeof(IGetAllService<,>), typeof(JsonApiResourceService<,>));

            _services.AddScoped(typeof(IGetByIdService<>), typeof(JsonApiResourceService<>));
            _services.AddScoped(typeof(IGetByIdService<,>), typeof(JsonApiResourceService<,>));

            _services.AddScoped(typeof(IGetRelationshipService<>), typeof(JsonApiResourceService<>));
            _services.AddScoped(typeof(IGetRelationshipService<,>), typeof(JsonApiResourceService<,>));

            _services.AddScoped(typeof(IGetSecondaryService<>), typeof(JsonApiResourceService<>));
            _services.AddScoped(typeof(IGetSecondaryService<,>), typeof(JsonApiResourceService<,>));

            _services.AddScoped(typeof(IUpdateService<>), typeof(JsonApiResourceService<>));
            _services.AddScoped(typeof(IUpdateService<,>), typeof(JsonApiResourceService<,>));

            _services.AddScoped(typeof(IDeleteService<>), typeof(JsonApiResourceService<>));
            _services.AddScoped(typeof(IDeleteService<,>), typeof(JsonApiResourceService<,>));

            _services.AddScoped(typeof(IResourceService<>), typeof(JsonApiResourceService<>));
            _services.AddScoped(typeof(IResourceService<,>), typeof(JsonApiResourceService<,>));

            _services.AddScoped(typeof(IResourceQueryService<,>), typeof(JsonApiResourceService<,>));
            _services.AddScoped(typeof(IResourceCommandService<,>), typeof(JsonApiResourceService<,>));
        }

        private void AddQueryStringParameterServices()
        {
            _services.AddScoped<IIncludeQueryStringParameterReader, IncludeQueryStringParameterReader>();
            _services.AddScoped<IFilterQueryStringParameterReader, FilterQueryStringParameterReader>();
            _services.AddScoped<ISortQueryStringParameterReader, SortQueryStringParameterReader>();
            _services.AddScoped<ISparseFieldSetQueryStringParameterReader, SparseFieldSetQueryStringParameterReader>();
            _services.AddScoped<IPaginationQueryStringParameterReader, PaginationQueryStringParameterReader>();
            _services.AddScoped<IDefaultsQueryStringParameterReader, DefaultsQueryStringParameterReader>();
            _services.AddScoped<INullsQueryStringParameterReader, NullsQueryStringParameterReader>();
            _services.AddScoped<IResourceDefinitionQueryableParameterReader, ResourceDefinitionQueryableParameterReader>();

            _services.AddScoped<IQueryStringParameterReader>(sp => sp.GetService<IIncludeQueryStringParameterReader>());
            _services.AddScoped<IQueryStringParameterReader>(sp => sp.GetService<IFilterQueryStringParameterReader>());
            _services.AddScoped<IQueryStringParameterReader>(sp => sp.GetService<ISortQueryStringParameterReader>());
            _services.AddScoped<IQueryStringParameterReader>(sp => sp.GetService<ISparseFieldSetQueryStringParameterReader>());
            _services.AddScoped<IQueryStringParameterReader>(sp => sp.GetService<IPaginationQueryStringParameterReader>());
            _services.AddScoped<IQueryStringParameterReader>(sp => sp.GetService<IDefaultsQueryStringParameterReader>());
            _services.AddScoped<IQueryStringParameterReader>(sp => sp.GetService<INullsQueryStringParameterReader>());
            _services.AddScoped<IQueryStringParameterReader>(sp => sp.GetService<IResourceDefinitionQueryableParameterReader>());

            _services.AddScoped<IQueryConstraintProvider>(sp => sp.GetService<IIncludeQueryStringParameterReader>());
            _services.AddScoped<IQueryConstraintProvider>(sp => sp.GetService<IFilterQueryStringParameterReader>());
            _services.AddScoped<IQueryConstraintProvider>(sp => sp.GetService<ISortQueryStringParameterReader>());
            _services.AddScoped<IQueryConstraintProvider>(sp => sp.GetService<ISparseFieldSetQueryStringParameterReader>());
            _services.AddScoped<IQueryConstraintProvider>(sp => sp.GetService<IPaginationQueryStringParameterReader>());
            _services.AddScoped<IQueryConstraintProvider>(sp => sp.GetService<IResourceDefinitionQueryableParameterReader>());

            _services.AddScoped<IQueryStringReader, QueryStringReader>();
            _services.AddSingleton<IRequestQueryStringAccessor, RequestQueryStringAccessor>();
        }

        private void AddResourceHooks()
        {
            _services.AddSingleton(typeof(IHooksDiscovery<>), typeof(HooksDiscovery<>));
            _services.AddScoped(typeof(IResourceHookContainer<>), typeof(ResourceDefinition<>));
            _services.AddTransient(typeof(IResourceHookExecutor), typeof(ResourceHookExecutor));
            _services.AddTransient<IHookExecutorHelper, HookExecutorHelper>();
            _services.AddTransient<ITraversalHelper, TraversalHelper>();
        }

        private void AddServerSerialization()
        {
            _services.AddScoped<IIncludedResourceObjectBuilder, IncludedResourceObjectBuilder>();
            _services.AddScoped<IJsonApiDeserializer, RequestDeserializer>();
            _services.AddScoped<IResourceObjectBuilderSettingsProvider, ResourceObjectBuilderSettingsProvider>();
            _services.AddScoped<IJsonApiSerializerFactory, ResponseSerializerFactory>();
            _services.AddScoped<ILinkBuilder, LinkBuilder>();
            _services.AddScoped(typeof(IMetaBuilder<>), typeof(MetaBuilder<>));
            _services.AddScoped(typeof(ResponseSerializer<>));
            _services.AddScoped(sp => sp.GetRequiredService<IJsonApiSerializerFactory>().GetSerializer());
            _services.AddScoped<IResourceObjectBuilder, ResponseResourceObjectBuilder>();
        }

        private void AddResourcesFromDbContext(DbContext dbContext, ResourceGraphBuilder builder)
        {
            foreach (var entityType in dbContext.Model.GetEntityTypes())
            {
                builder.Add(entityType.ClrType);
            }
        }
        
        public void Dispose()
        {
            _intermediateProvider.Dispose();
        }
    }
}
