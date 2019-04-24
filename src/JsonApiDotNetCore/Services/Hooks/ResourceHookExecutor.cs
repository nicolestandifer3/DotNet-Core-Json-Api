using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JsonApiDotNetCore.Internal;
using JsonApiDotNetCore.Internal.Generics;
using JsonApiDotNetCore.Models;


namespace JsonApiDotNetCore.Services
{


    /// <inheritdoc/>
    public class ResourceHookExecutor<TEntity> : IResourceHookExecutor<TEntity> where TEntity : class, IIdentifiable
    {
        protected readonly ResourceHook[] _implementedHooks;
        protected readonly IJsonApiContext _jsonApiContext;
        protected readonly IGenericProcessorFactory _genericProcessorFactory;
        protected readonly ResourceDefinition<TEntity> _resourceDefinition;
        protected readonly IResourceGraph _graph;
        protected readonly Type _entityType;
        protected readonly IMetaHookExecutor _meta;
        protected readonly ResourceAction[] _singleActions;
        protected readonly Type _openContainerType;
        protected Dictionary<Type, HashSet<IIdentifiable>> _processedEntities;



        public ResourceHookExecutor(
            IJsonApiContext jsonApiContext,
            IHooksDiscovery<TEntity> hooksConfiguration,
            IMetaHookExecutor meta
            )
        {
            _genericProcessorFactory = jsonApiContext.GenericProcessorFactory;
            _jsonApiContext = jsonApiContext;
            _graph = _jsonApiContext.ResourceGraph;
            _meta = meta;
            _implementedHooks = hooksConfiguration.ImplementedHooks;
            _entityType = typeof(TEntity);
            _openContainerType = typeof(ResourceDefinition<>);
            _processedEntities = new Dictionary<Type, HashSet<IIdentifiable>>();

            _singleActions = new ResourceAction[]
                {
                    ResourceAction.GetSingle,
                    ResourceAction.Create,
                    ResourceAction.Delete,
                    ResourceAction.Patch,
                    ResourceAction.GetRelationship,
                    ResourceAction.PatchRelationship
                };
        }

        public virtual IEnumerable<TEntity> BeforeCreate(IEnumerable<TEntity> entities, ResourceAction actionSource)
        {
            var hookContainer = _meta.GetResourceHookContainer<TEntity>(ResourceHook.BeforeCreate);

            /// traversing the 0th layer. Not including this in the recursive function
            /// because the most complexities that arrise in the tree traversal do not
            /// apply to the 0th layer (eg non-homogeneity of the next layers)
            if (hookContainer != null)
            {
                RegisterProcessedEntities(entities);
                var parsedEntities = hookContainer.BeforeCreate(entities, actionSource); // eg all of type {Article}
                ValidateHookResponse(parsedEntities, actionSource);
                entities = parsedEntities;
            }



            /// We use IIdentifiable instead of TEntity, because deeper layers
            /// in the tree traversal will not necessarily be homogenous (i.e. 
            /// not all elements will be some same type T).
            /// eg: this list will be all of type {Article}, but deeper layers 
            /// could consist of { Tag, Author, Comment }
            _meta.UpdateMetaInformation(new Type[] { _entityType }, ResourceHook.BeforeUpdate);
            BreadthFirstTraverse(entities, (container, relatedEntities) =>
            {
                return CallHook(container, ResourceHook.BeforeUpdate, new object[] { relatedEntities, actionSource });
            });

            FlushRegister();
            return entities;
        }

        /// <inheritdoc/>
        public virtual IEnumerable<TEntity> AfterCreate(IEnumerable<TEntity> entities, ResourceAction actionSource)
        {
            RegisterProcessedEntities(entities);
            var hookContainer = _meta.GetResourceHookContainer<TEntity>(ResourceHook.AfterCreate);
            /// @TODO: even if we don't have an implementation for eg TodoItem AfterCreate, 
            /// we should still consider to fire the hooks of its relation, eg TodoItem.Owner

            _meta.UpdateMetaInformation(new Type[] { _entityType }, ResourceHook.AfterUpdate);
            BreadthFirstTraverse(entities, (container, relatedEntities) =>
            {
                return CallHook(container, ResourceHook.AfterUpdate, new object[] { relatedEntities, actionSource });
            });

            if (hookContainer != null)
            {
                var parsedEntities = hookContainer.AfterCreate(entities, actionSource);
                ValidateHookResponse(parsedEntities, actionSource);
                entities = parsedEntities;
            }

            FlushRegister();
            return entities;
        }

        /// <inheritdoc/>
        public virtual void BeforeRead(ResourceAction actionSource, string stringId = null)
        {
            var hookContainer = _meta.GetResourceHookContainer<TEntity>(ResourceHook.BeforeRead);
            hookContainer?.BeforeRead(actionSource, stringId);
            FlushRegister();
        }

        /// <inheritdoc/>
        public virtual IEnumerable<TEntity> AfterRead(IEnumerable<TEntity> entities, ResourceAction actionSource)
        {
            RegisterProcessedEntities(entities);
            var hookContainer = _meta.GetResourceHookContainer<TEntity>(ResourceHook.AfterRead);

            _meta.UpdateMetaInformation(new Type[] { _entityType }, new ResourceHook[] { ResourceHook.AfterRead, ResourceHook.BeforeRead });
            BreadthFirstTraverse(entities, (container, relatedEntities) =>
            {
                if (container.ShouldExecuteHook(ResourceHook.BeforeRead))
                    CallHook(container, ResourceHook.BeforeRead, new object[] { actionSource, default(string) });

                if (container.ShouldExecuteHook(ResourceHook.AfterRead))
                {
                    return CallHook(container, ResourceHook.AfterRead, new object[] { relatedEntities, actionSource });
                }
                return relatedEntities;
            });

            if (hookContainer != null)
            {
                var parsedEntities = hookContainer.AfterRead(entities, actionSource);
                ValidateHookResponse(parsedEntities, actionSource);
                entities = parsedEntities;
            }

            FlushRegister();
            return entities;
        }
        /// <inheritdoc/>
        public virtual IEnumerable<TEntity> BeforeUpdate(IEnumerable<TEntity> entities, ResourceAction actionSource)
        {
            var hookContainer = _meta.GetResourceHookContainer<TEntity>(ResourceHook.BeforeUpdate);
            if (hookContainer != null)
            {
                RegisterProcessedEntities(entities);
                var parsedEntities = hookContainer.BeforeUpdate(entities, actionSource);
                ValidateHookResponse(parsedEntities, actionSource);
                entities = parsedEntities;
            }

            _meta.UpdateMetaInformation(new Type[] { _entityType }, ResourceHook.BeforeUpdate);
            BreadthFirstTraverse(entities, (container, relatedEntities) =>
            {
                return CallHook(container, ResourceHook.BeforeUpdate, new object[] { relatedEntities, actionSource });
            });

            FlushRegister();
            return entities;
        }

        /// <inheritdoc/>
        public virtual IEnumerable<TEntity> AfterUpdate(IEnumerable<TEntity> entities, ResourceAction actionSource)
        {
            RegisterProcessedEntities(entities);
            var hookContainer = _meta.GetResourceHookContainer<TEntity>(ResourceHook.AfterUpdate);

            _meta.UpdateMetaInformation(new Type[] { _entityType }, ResourceHook.AfterUpdate);
            BreadthFirstTraverse(entities, (container, relatedEntities) =>
            {
                return CallHook(container, ResourceHook.AfterUpdate, new object[] { relatedEntities, actionSource });
            });

            if (hookContainer != null)
            {
                var parsedEntities = hookContainer.AfterUpdate(entities, actionSource);
                ValidateHookResponse(parsedEntities, actionSource);
                entities = parsedEntities;
            }

            FlushRegister();
            return entities;
        }

        /// <inheritdoc/>
        public virtual void BeforeDelete(IEnumerable<TEntity> entities, ResourceAction actionSource)
        {
            var hookContainer = _meta.GetResourceHookContainer<TEntity>(ResourceHook.BeforeDelete);
            hookContainer?.BeforeDelete(entities, actionSource);
            FlushRegister();
        }

        /// <inheritdoc/>
        public virtual void AfterDelete(IEnumerable<TEntity> entities, bool succeeded, ResourceAction actionSource)
        {
            var hookContainer = _meta.GetResourceHookContainer<TEntity>(ResourceHook.AfterDelete);
            hookContainer?.AfterDelete(entities, succeeded, actionSource);
            FlushRegister();
        }

        /// <summary>
        /// Fires the hooks for related (nested) entities.
        /// Performs a recursive, forward-looking breadth first traversal 
        /// through the entitis in <paramref name="currentLayer"/> and fires the 
        /// associated resource hooks 
        /// </summary>
        /// <param name="currentLayer">Current layer.</param>
        /// <param name="hookExecutionAction">Hook execution action.</param>
        void BreadthFirstTraverse(
            IEnumerable<IIdentifiable> currentLayer,
            Func<IResourceHookContainer, object, object> hookExecutionAction
            )
        {
            // for the entities in the current layer: get the collection of all related entities
            var relationshipsInCurrentLayer = ExtractionLoop(currentLayer);

            if (!relationshipsInCurrentLayer.Any()) return;

            // for the unique set of entities in that collection, execute the hooks
            ExecutionLoop(relationshipsInCurrentLayer, hookExecutionAction);
            // for the entities in the current layer: reassign relationships where needed.
            AssignmentLoop(currentLayer, relationshipsInCurrentLayer);

            var nextLayer = relationshipsInCurrentLayer.Values.SelectMany(tuple => tuple.Item2);
            if (nextLayer.Any())
            {
                var uniqueTypesInNextLayer = relationshipsInCurrentLayer.Values.SelectMany(tuple => tuple.Item1.Select(proxy => proxy.TargetType));
                _meta.UpdateMetaInformation(uniqueTypesInNextLayer);
                BreadthFirstTraverse(nextLayer, hookExecutionAction);
            }
        }

        /// <summary>
        /// Iterates through the entities in the current layer. This layer can be inhomogeneous.
        /// For each of these entities: gets all related entity  for which we want to 
        /// execute a hook (target entities), this is defined in MetaInfo.
        /// Grouped per relation, stores these target in relationshipsInCurrentLayer
        /// </summary>
        /// <returns>Hook targets for current layer.</returns>
        /// <param name="currentLayer">Current layer.</param>
        Dictionary<Type, (List<RelationshipProxy>, HashSet<IIdentifiable>)> ExtractionLoop(
            IEnumerable<IIdentifiable> currentLayer
            )
        {
            var relationshipsInCurrentLayer = new Dictionary<Type, (List<RelationshipProxy>, HashSet<IIdentifiable>)>();
            foreach (IIdentifiable currentLayerEntity in currentLayer)
            {
                foreach (RelationshipProxy proxy in _meta.GetMetaEntries(currentLayerEntity))
                {
                    var relationshipValue = proxy.GetValue(currentLayerEntity);
                    // skip iteration if there is no relation assigned
                    if (relationshipValue == null) continue;
                    if (!(relationshipValue is IEnumerable<IIdentifiable> relatedEntities))
                    {
                        // in the case of a to-one relationship, the assigned value
                        // will not be a list. We therefore first wrap it in a list.
                        var list = TypeHelper.CreateListFor(relationshipValue.GetType());
                        list.Add(relationshipValue);
                        relatedEntities = (IEnumerable<IIdentifiable>)list;
                    }

                    // filter the retrieved related entities collection against the entities that were processed in previous iterations
                    var newEntitiesInTree = UniqueInTree(relatedEntities, proxy.TargetType);
                    if (!newEntitiesInTree.Any()) continue;
                    if (!relationshipsInCurrentLayer.ContainsKey(proxy.ParentType))
                    {
                        relationshipsInCurrentLayer[proxy.ParentType] = (new List<RelationshipProxy>() { proxy }, newEntitiesInTree);
                    }
                    else
                    {
                        (var proxies, var entities) = relationshipsInCurrentLayer[proxy.ParentType];
                        entities.UnionWith(newEntitiesInTree);
                        if (!proxies.Select(p => p.RelationshipIdentifier).Contains(proxy.RelationshipIdentifier)) proxies.Add(proxy);
                    }
                }
            }
            return relationshipsInCurrentLayer;
        }


        /// <summary>
        /// Executes the hooks for every key in relationshipsInCurrentLayer,
        /// </summary>
        /// <param name="relationshipsInCurrentLayer">Hook targets for current layer.</param>
        /// <param name="hookExecution">Hook execution method.</param>
        void ExecutionLoop(
            Dictionary<Type, (List<RelationshipProxy>, HashSet<IIdentifiable>)> relationshipsInCurrentLayer,
            Func<IResourceHookContainer, object, object> hookExecution
            )
        {
            // note that it is possible that we have multiple relations to one type.
            var parentTypes = relationshipsInCurrentLayer.Keys.ToArray();

            foreach (Type type in parentTypes)
            {
                (var relationshipsProxy, var relatedEntities) = relationshipsInCurrentLayer[type];
                var targetType = relationshipsProxy.First().TargetType;
                var hookContainer = _meta.GetResourceHookContainer(targetType);
                var castedEntities = TypeHelper.ConvertCollection(relatedEntities, targetType);
                var filteredEntities = ((IEnumerable)hookExecution(hookContainer, castedEntities)).Cast<IIdentifiable>().ToList();
                relationshipsInCurrentLayer[type] = (relationshipsProxy, new HashSet<IIdentifiable>(filteredEntities));
            }
        }

        /// <summary>
        /// When this method is called, the values in relationshipsInCurrentLayer
        /// will contain a subset compared to in the DoExtractionLoop call.
        /// We now need to iterate through currentLayer again and remove any of 
        /// their related entities that do not occur in relationshipsInCurrentLayer
        /// </summary>
        /// <param name="currentLayer">Entities in current layer.</param>
        /// <param name="relationshipsInCurrentLayer">Hook targets for current layer.</param>
        void AssignmentLoop(
            IEnumerable<IIdentifiable> currentLayer,
            Dictionary<Type, (List<RelationshipProxy>, HashSet<IIdentifiable>)> relationshipsInCurrentLayer
            )
        {

            foreach (IIdentifiable currentLayerEntity in currentLayer)
            {
                foreach (RelationshipProxy proxy in _meta.GetMetaEntries(currentLayerEntity))
                {

                    /// if there are no related entities included for 
                    /// currentLayerEntity for this relation, then this key will 
                    /// not exist, and we may continue to the next.
                    if (!relationshipsInCurrentLayer.TryGetValue(proxy.ParentType, out var tuple))
                    {
                        continue;
                    }
                    var parsedEntities = tuple.Item2;

                    var relationshipValue = proxy.GetValue(currentLayerEntity);
                    if (relationshipValue is IEnumerable<IIdentifiable> relationshipCollection)
                    {
                        var convertedCollection = TypeHelper.ConvertCollection(relationshipCollection.Intersect(parsedEntities), proxy.TargetType);
                        proxy.SetValue(currentLayerEntity, convertedCollection);
                    }
                    else if (relationshipValue is IIdentifiable relationshipSingle)
                    {
                        if (!parsedEntities.Contains(relationshipValue))
                        {
                            proxy.SetValue(currentLayerEntity, null);
                        }
                    }
                }

            }
        }

        /// <summary>
        /// checks that the collection does not contain more than one item when
        /// relevant (eg AfterRead from GetSingle pipeline).
        /// </summary>
        /// <param name="returnedList"> The collection returned from the hook</param>
        /// <param name="actionSource">The pipeine from which the hook was fired</param>
        protected void ValidateHookResponse(object returnedList, ResourceAction actionSource = 0)
        {
        
            if (actionSource != ResourceAction.None && _singleActions.Contains(actionSource) && ((IList)returnedList).Count > 1)
            {
                throw new ApplicationException("The returned collection from this hook may only contain one item in the case of the" +
                    actionSource.ToString() + "pipeline");
            }
        }

        /// <summary>
        /// Registers the processed entities in the dictionary grouped by type
        /// </summary>
        /// <param name="entities">Entities to register</param>
        /// <param name="entityType">Entity type.</param>
        void RegisterProcessedEntities(IEnumerable<IIdentifiable> entities, Type entityType)
        {
            var processedEntities = GetProcessedEntities(entityType);
            processedEntities.UnionWith(new HashSet<IIdentifiable>(entities));
        }
        void RegisterProcessedEntities(IEnumerable<TEntity> entities)
        {
            RegisterProcessedEntities(entities, _entityType);
        }


        /// <summary>
        /// Gets the processed entities for a given type, instantiates the collection if new.
        /// </summary>
        /// <returns>The processed entities.</returns>
        /// <param name="entityType">Entity type.</param>
        HashSet<IIdentifiable> GetProcessedEntities(Type entityType)
        {

            if (!_processedEntities.TryGetValue(entityType, out HashSet<IIdentifiable> processedEntities))
            {
                processedEntities = new HashSet<IIdentifiable>();
                _processedEntities[entityType] = processedEntities;
            }
            return processedEntities;

        }

        /// <summary>
        /// Using the register of processed entities, determines the unique and new
        /// entities with respect to previous iterations.
        /// </summary>
        /// <returns>The in tree.</returns>
        /// <param name="entities">Entities.</param>
        /// <param name="entityType">Entity type.</param>
        HashSet<IIdentifiable> UniqueInTree(IEnumerable<IIdentifiable> entities, Type entityType)
        {
            var newEntities = new HashSet<IIdentifiable>(entities.Except(GetProcessedEntities(entityType)));
            RegisterProcessedEntities(entities, entityType);
            return newEntities;
        }


        /// <summary>
        /// A method that reflectively calls a resource hook.
        /// TODO:  I tried casting IResourceHookContainer container to type
        /// IResourceHookContainer{IIdentifiable}, which would have allowed us
        /// to call the hook on the nested containers normally, but I believe
        /// this is not possible. We therefore need this helper method.
        /// </summary>
        /// <returns>The hook.</returns>
        /// <param name="container">Container for related entity.</param>
        /// <param name="hook">Target hook type.</param>
        /// <param name="arguments">Arguments to call the hook with.</param>
        object CallHook(IResourceHookContainer container, ResourceHook hook, object[] arguments)
        {
            var method = container.GetType().GetMethods().First(m => m.Name == hook.ToString("G"));
            return method.Invoke(container, arguments);
        }

        /// <summary>
        /// We need to flush the list of processed entities because typically
        /// the hook executor will be caled twice per service pipeline (eg BeforeCreate
        /// and AfterCreate).
        /// </summary>
        void FlushRegister()
        {

            _processedEntities = new Dictionary<Type, HashSet<IIdentifiable>>();
        }
    }
}

