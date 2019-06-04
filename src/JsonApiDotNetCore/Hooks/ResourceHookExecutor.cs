using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JsonApiDotNetCore.Internal;
using JsonApiDotNetCore.Models;
using PrincipalType = System.Type;
using DependentType = System.Type;
using JsonApiDotNetCore.Services;
using JsonApiDotNetCore.Extensions;

namespace JsonApiDotNetCore.Hooks
{
    /// <inheritdoc/>
    internal class ResourceHookExecutor : IResourceHookExecutor
    {
        public static readonly IdentifiableComparer Comparer = new IdentifiableComparer();
        internal readonly TraversalHelper _traversalHelper;
        internal readonly IHookExecutorHelper _executorHelper;
        protected readonly IJsonApiContext _context;
        private readonly IResourceGraph _graph;

        public ResourceHookExecutor(IHookExecutorHelper helper, IJsonApiContext context, IResourceGraph graph)
        {
            _executorHelper = helper;
            _context = context;
            _graph = graph;
            _traversalHelper = new TraversalHelper(graph, _context);
        }

        /// <inheritdoc/>
        public virtual void BeforeRead<TEntity>(ResourcePipeline pipeline, string stringId = null) where TEntity : class, IIdentifiable
        {
            var hookContainer = _executorHelper.GetResourceHookContainer<TEntity>(ResourceHook.BeforeRead);
            hookContainer?.BeforeRead(pipeline, false, stringId);
            var contextEntity = _graph.GetContextEntity(typeof(TEntity));
            var calledContainers = new List<PrincipalType>() { typeof(TEntity) };
            foreach (var relationshipPath in _context.IncludedRelationships)
            {
                RecursiveBeforeRead(contextEntity, relationshipPath.Split('.').ToList(), pipeline, calledContainers);
            }
        }

        /// <inheritdoc/>
        public virtual IEnumerable<TEntity> BeforeUpdate<TEntity>(IEnumerable<TEntity> entities, ResourcePipeline pipeline) where TEntity : class, IIdentifiable
        {
            if (GetHook(ResourceHook.BeforeUpdate, entities, out var container, out var node))
            {
                var dbValues = _executorHelper.LoadDbValues((IEnumerable<TEntity>)node.UniqueEntities, ResourceHook.BeforeUpdate, node.RelationshipsToNextLayer);
                var diff = new EntityDiff<TEntity>(node.UniqueEntities, dbValues, node.PrincipalsToNextLayer());
                IEnumerable<TEntity> updated = container.BeforeUpdate(diff, pipeline);
                node.UpdateUnique(updated);
                node.Reassign(entities);
            }

            FireNestedBeforeUpdateHooks(pipeline, _traversalHelper.CreateNextLayer(node));
            return entities;
        }

        /// <inheritdoc/>
        public virtual IEnumerable<TEntity> BeforeCreate<TEntity>(IEnumerable<TEntity> entities, ResourcePipeline pipeline) where TEntity : class, IIdentifiable
        {
            if (GetHook(ResourceHook.BeforeCreate, entities, out var container, out var node))
            {
                IEnumerable<TEntity> updated = container.BeforeCreate((HashSet<TEntity>)node.UniqueEntities, pipeline);
                node.UpdateUnique(updated);
                node.Reassign(entities);
            }

            FireNestedBeforeUpdateHooks(pipeline, _traversalHelper.CreateNextLayer(node));
            return entities;
        }

        /// <inheritdoc/>
        public virtual IEnumerable<TEntity> BeforeDelete<TEntity>(IEnumerable<TEntity> entities, ResourcePipeline pipeline) where TEntity : class, IIdentifiable
        {
            if (GetHook(ResourceHook.BeforeDelete, entities, out var container, out var node))
            {
                IEnumerable<TEntity> updated = container.BeforeDelete((HashSet<TEntity>)node.UniqueEntities, pipeline);
                node.UpdateUnique(updated);
                node.Reassign(entities);
            }

            foreach (var entry in node.PrincipalsToNextLayerByType())
            {
                var dependentType = entry.Key;
                var implicitTargets = entry.Value;
                FireForAffectedImplicits(dependentType, implicitTargets, pipeline);
            }
            return entities;
        }

        /// <inheritdoc/>
        public virtual IEnumerable<TEntity> OnReturn<TEntity>(IEnumerable<TEntity> entities, ResourcePipeline pipeline) where TEntity : class, IIdentifiable
        {
            if (GetHook(ResourceHook.OnReturn, entities, out var container, out var node) && pipeline != ResourcePipeline.GetRelationship)
            {
                IEnumerable<TEntity> updated = container.OnReturn((HashSet<TEntity>)node.UniqueEntities, pipeline);
                ValidateHookResponse(updated);
                node.UpdateUnique(updated);
                node.Reassign(entities);
            }

            Traverse(_traversalHelper.CreateNextLayer(node), ResourceHook.OnReturn, (nextContainer, nextNode) =>
            {
                var filteredUniqueSet = CallHook(nextContainer, ResourceHook.OnReturn, new object[] { nextNode.UniqueEntities, pipeline });
                nextNode.UpdateUnique(filteredUniqueSet);
                nextNode.Reassign();
            });
            return entities;
        }

        /// <inheritdoc/>
        public virtual void AfterRead<TEntity>(IEnumerable<TEntity> entities, ResourcePipeline pipeline) where TEntity : class, IIdentifiable
        {
            if (GetHook(ResourceHook.AfterRead, entities, out var container, out var node))
            {
                container.AfterRead((HashSet<TEntity>)node.UniqueEntities, pipeline);
            }

            Traverse(_traversalHelper.CreateNextLayer(node), ResourceHook.AfterRead, (nextContainer, nextNode) =>
            {
                CallHook(nextContainer, ResourceHook.AfterRead, new object[] { nextNode.UniqueEntities, pipeline, true });
            });
        }

        /// <inheritdoc/>
        public virtual void AfterCreate<TEntity>(IEnumerable<TEntity> entities, ResourcePipeline pipeline) where TEntity : class, IIdentifiable
        {
            if (GetHook(ResourceHook.AfterCreate, entities, out var container, out var node))
            {
                container.AfterCreate((HashSet<TEntity>)node.UniqueEntities, pipeline);
            }

            Traverse(_traversalHelper.CreateNextLayer(node),
                ResourceHook.AfterUpdateRelationship,
                (nextContainer, nextNode) => FireAfterUpdateRelationship(nextContainer, nextNode, pipeline));
        }

        /// <inheritdoc/>
        public virtual void AfterUpdate<TEntity>(IEnumerable<TEntity> entities, ResourcePipeline pipeline) where TEntity : class, IIdentifiable
        {
            if (GetHook(ResourceHook.AfterUpdate, entities, out var container, out var node))
            {
                container.AfterUpdate((HashSet<TEntity>)node.UniqueEntities, pipeline);
            }

            Traverse(_traversalHelper.CreateNextLayer(node),
                ResourceHook.AfterUpdateRelationship,
                (nextContainer, nextNode) => FireAfterUpdateRelationship(nextContainer, nextNode, pipeline));
        }

        /// <inheritdoc/>
        public virtual void AfterDelete<TEntity>(IEnumerable<TEntity> entities, ResourcePipeline pipeline, bool succeeded) where TEntity : class, IIdentifiable
        {
            if (GetHook(ResourceHook.AfterDelete, entities, out var container, out var node))
            {
                container.AfterDelete((HashSet<TEntity>)node.UniqueEntities, pipeline, succeeded);
            }
        }

        /// <summary>
        /// For a given <see cref="ResourceHook"/> target and for a given type 
        /// <typeparamref name="TEntity"/>, gets the hook container if the target
        /// hook was implemented and should be executed.
        /// <para />
        /// Along the way, creates a traversable node from the root entity set.
        /// </summary>
        /// <returns><c>true</c>, if hook was implemented, <c>false</c> otherwise.</returns>
        bool GetHook<TEntity>(ResourceHook target, IEnumerable<TEntity> entities,
            out IResourceHookContainer<TEntity> container,
            out RootNode<TEntity> node) where TEntity : class, IIdentifiable
        {
            node = _traversalHelper.CreateRootNode(entities);
            container = _executorHelper.GetResourceHookContainer<TEntity>(target);
            return container != null;
        }

        /// <summary>
        /// Traverses the nodes in a <see cref="EntityChildLayer"/>.
        /// </summary>
        void Traverse(EntityChildLayer currentLayer, ResourceHook target, Action<IResourceHookContainer, IEntityNode> action)
        {
            if (!currentLayer.AnyEntities()) return;
            foreach (IEntityNode node in currentLayer)
            {
                var entityType = node.EntityType;
                var hookContainer = _executorHelper.GetResourceHookContainer(entityType, target);
                if (hookContainer == null) continue;
                action(hookContainer, node);
            }

            Traverse(_traversalHelper.CreateNextLayer(currentLayer.ToList()), target, action);
        }

        /// <summary>
        /// Recursively goes through the included relationships from JsonApiContext,
        /// translates them to the corresponding hook containers and fires the 
        /// BeforeRead hook (if implemented)
        /// </summary>
        void RecursiveBeforeRead(ContextEntity contextEntity, List<string> relationshipChain, ResourcePipeline pipeline, List<PrincipalType> calledContainers)
        {
            var target = relationshipChain.First();
            var relationship = contextEntity.Relationships.FirstOrDefault(r => r.PublicRelationshipName == target);
            if (relationship == null)
            {
                throw new JsonApiException(400, $"Invalid relationship {target} on {contextEntity.EntityName}",
                    $"{contextEntity.EntityName} does not have a relationship named {target}");
            }

            if (!calledContainers.Contains(relationship.DependentType))
            {
                calledContainers.Add(relationship.DependentType);
                var container = _executorHelper.GetResourceHookContainer(relationship.DependentType, ResourceHook.BeforeRead);
                if (container != null)
                {
                    CallHook(container, ResourceHook.BeforeRead, new object[] { pipeline, true, null });
                }
            }
            relationshipChain.RemoveAt(0);
            if (relationshipChain.Any())
            {

                RecursiveBeforeRead(_graph.GetContextEntity(relationship.DependentType), relationshipChain, pipeline, calledContainers);
            }
        }

        /// <summary>
        /// Fires the nested before hooks. For example consider the case when
        /// the owner of an article a1 (one-to-one) was updated from o1 to o2, where o2
        /// was already related to a2. Then, the BeforeUpdateRelationship should be
        /// fired for o2, and the BeforeImplicitUpdateRelationship hook should be fired for
        /// o2 and then too for a2.
        /// </summary>
        void FireNestedBeforeUpdateHooks(ResourcePipeline pipeline, EntityChildLayer layer)
        {
            foreach (IEntityNode node in layer)
            {
                var nestedHookcontainer = _executorHelper.GetResourceHookContainer(node.EntityType, ResourceHook.BeforeUpdateRelationship);
                IEnumerable uniqueEntities = node.UniqueEntities;
                DependentType entityType = node.EntityType;

                // fire the BeforeUpdateRelationship hook for o1
                if (nestedHookcontainer != null)
                {
                    if (uniqueEntities.Cast<IIdentifiable>().Any())
                    {
                        var dbValues = _executorHelper.LoadDbValues(entityType, entityType, uniqueEntities, ResourceHook.BeforeUpdateRelationship, node.RelationshipsToNextLayer);
                        var resourcesByRelationship = CreateRelationshipHelper(entityType, node.RelationshipsFromPreviousLayer.GetDependentEntities(), dbValues);
                        var allowedIds = CallHook(nestedHookcontainer, ResourceHook.BeforeUpdateRelationship, new object[] { GetIds(uniqueEntities), resourcesByRelationship, pipeline }).Cast<string>();
                        var updated = GetAllowedEntities(uniqueEntities, allowedIds);
                        node.UpdateUnique(updated);
                        node.Reassign();
                    }
                }

                // fire the BeforeImplicitUpdateRelationship hook for o1
                var implicitPrincipalTargets = node.RelationshipsFromPreviousLayer.GetPrincipalEntities();
                if (pipeline != ResourcePipeline.Post && implicitPrincipalTargets.Any())
                {
                    FireForAffectedImplicits(entityType, implicitPrincipalTargets, pipeline, uniqueEntities);
                }

                // fire the BeforeImplicitUpdateRelationship hook for a2
                var dependentEntities = node.RelationshipsFromPreviousLayer.GetDependentEntities();
                if (dependentEntities.Any())
                {
                    (var implicitDependentTargets, var principalEntityType) = GetDependentImplicitsTargets(dependentEntities);
                    FireForAffectedImplicits(principalEntityType, implicitDependentTargets, pipeline);
                }
            }
        }

        /// <summary>
        /// Given a source of entities, gets the implicitly affected entities 
        /// from the database and calls the BeforeImplicitUpdateRelationship hook.
        /// </summary>
        void FireForAffectedImplicits(Type entityTypeToInclude, Dictionary<RelationshipProxy, IEnumerable> implicitsTarget, ResourcePipeline pipeline, IEnumerable existingImplicitEntities = null)
        {
            var container = _executorHelper.GetResourceHookContainer(entityTypeToInclude, ResourceHook.BeforeImplicitUpdateRelationship);
            if (container == null) return;
            var implicitAffected = _executorHelper.LoadImplicitlyAffected(implicitsTarget, existingImplicitEntities);
            if (!implicitAffected.Any()) return;
            var resourcesByRelationship = CreateRelationshipHelper(entityTypeToInclude, implicitAffected);
            CallHook(container, ResourceHook.BeforeImplicitUpdateRelationship, new object[] { resourcesByRelationship, pipeline, });
        }

        /// <summary>
        /// checks that the collection does not contain more than one item when
        /// relevant (eg AfterRead from GetSingle pipeline).
        /// </summary>
        /// <param name="returnedList"> The collection returned from the hook</param>
        /// <param name="pipeline">The pipeine from which the hook was fired</param>
        void ValidateHookResponse<T>(IEnumerable<T> returnedList, ResourcePipeline pipeline = 0)
        {
            if (pipeline == ResourcePipeline.GetSingle && returnedList.Count() > 1)
            {
                throw new ApplicationException("The returned collection from this hook may contain at most one item in the case of the" +
                    pipeline.ToString("G") + "pipeline");
            }
        }

        /// <summary>
        /// NOTE: in JADNC usage, the root layer is ALWAYS homogenous, so we can be sure that for every 
        /// relationship to the previous layer, the principal type is the same.
        /// </summary>
        (Dictionary<RelationshipProxy, IEnumerable>, PrincipalType) GetDependentImplicitsTargets(Dictionary<RelationshipProxy, IEnumerable> dependentEntities)
        {
            PrincipalType principalType = dependentEntities.First().Key.PrincipalType;
            var byInverseRelationship = dependentEntities.Where(kvp => kvp.Key.Attribute.InverseNavigation != null).ToDictionary(kvp => GetInverseRelationship(kvp.Key), kvp => kvp.Value);
            return (byInverseRelationship, principalType);

        }

        /// <summary>
        /// A helper method to call a hook on <paramref name="container"/> reflectively.
        /// </summary>
        IEnumerable CallHook(IResourceHookContainer container, ResourceHook hook, object[] arguments)
        {
            var method = container.GetType().GetMethod(hook.ToString("G"));
            // note that some of the hooks return "void". When these hooks, the 
            // are called reflectively with Invoke like here, the return value
            // is just null, so we don't have to worry about casting issues here.
            return (IEnumerable)ThrowJsonApiExceptionOnError(() => method.Invoke(container, arguments));
        }

        /// <summary>
        /// If the <see cref="CallHook"/> method, unwrap and throw the actual exception.
        /// </summary>
        object ThrowJsonApiExceptionOnError(Func<object> action)
        {
            try
            {
                return action();
            }
            catch (TargetInvocationException tie)
            {
                throw tie.InnerException;
            }
        }

        /// <summary>
        /// Helper method to instantiate UpdatedRelationshipHelper for a given <paramref name="entityType"/>
        /// If <paramref name="dbValues"/> are included, the values of the entries in <paramref name="prevLayerRelationships"/> need to be replaced with these values.
        /// </summary>
        /// <returns>The relationship helper.</returns>
        IAffectedRelationships CreateRelationshipHelper(DependentType entityType, Dictionary<RelationshipProxy, IEnumerable> prevLayerRelationships, IEnumerable dbValues = null)
        {
            if (dbValues != null) ReplaceWithDbValues(prevLayerRelationships, dbValues.Cast<IIdentifiable>());
            return (IAffectedRelationships)TypeHelper.CreateInstanceOfOpenType(typeof(UpdatedRelationshipHelper<>), entityType, prevLayerRelationships);
        }

        /// <summary>
        /// Replaces the entities in the values of the prevLayerRelationships dictionary 
        /// with the corresponding entities loaded from the db.
        /// </summary>
        void ReplaceWithDbValues(Dictionary<RelationshipProxy, IEnumerable> prevLayerRelationships, IEnumerable<IIdentifiable> dbValues)
        {
            foreach (var key in prevLayerRelationships.Keys.ToList())
            {
                var replaced = prevLayerRelationships[key].Cast<IIdentifiable>().Select(entity => dbValues.Single(dbEntity => dbEntity.StringId == entity.StringId)).Cast(key.DependentType);
                prevLayerRelationships[key] = replaced;
            }
        }

        /// <summary>
        /// Fitler the source set by removing the entities with id that are not 
        /// in <paramref name="allowedIds"/>.
        /// </summary>
        HashSet<IIdentifiable> GetAllowedEntities(IEnumerable source, IEnumerable<string> allowedIds)
        {
            return new HashSet<IIdentifiable>(source.Cast<IIdentifiable>().Where(ue => allowedIds.Contains(ue.StringId)));
        }

        /// <summary>
        /// Gets the inverse <see cref="RelationshipProxy"/> for <paramref name="proxy"/>
        /// </summary>
        RelationshipProxy GetInverseRelationship(RelationshipProxy proxy)
        {
            return new RelationshipProxy(_graph.GetInverseRelationship(proxy.Attribute), proxy.PrincipalType, false);
        }

        void FireAfterUpdateRelationship(IResourceHookContainer container, IEntityNode node, ResourcePipeline pipeline)
        {
            var resourcesByRelationship = CreateRelationshipHelper(node.EntityType, node.RelationshipsFromPreviousLayer.GetDependentEntities());
            CallHook(container, ResourceHook.AfterUpdateRelationship, new object[] { resourcesByRelationship, pipeline });
        }

        HashSet<string> GetIds(IEnumerable entities)
        {
            return new HashSet<string>(entities.Cast<IIdentifiable>().Select(e => e.StringId));
        }
    }
}

