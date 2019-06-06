﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JsonApiDotNetCore.Extensions;
using JsonApiDotNetCore.Internal;
using JsonApiDotNetCore.Models;
using JsonApiDotNetCore.Services;
using DependentType = System.Type;
using PrincipalType = System.Type;

namespace JsonApiDotNetCore.Hooks
{
    /// <summary>
    /// A helper class used by the <see cref="ResourceHookExecutor"/> to traverse through
    /// entity data structures (trees), allowing for a breadth-first-traversal
    /// 
    /// It creates nodes for each layer. 
    /// Typically, the first layer is homogeneous (all entities have the same type),
    /// and further nodes can be mixed.
    /// 
    /// </summary>
    internal class TraversalHelper
    {
        private readonly IResourceGraph _graph;
        private readonly IJsonApiContext _context;
        /// <summary>
        /// Keeps track of which entities has already been traversed through, to prevent
        /// infinite loops in eg cyclic data structures.
        /// </summary>
        private Dictionary<DependentType, HashSet<IIdentifiable>> _processedEntities;
        /// <summary>
        /// A mapper from <see cref="RelationshipAttribute"/> to <see cref="RelationshipProxy"/>. 
        /// See the latter for more details.
        /// </summary>
        private readonly Dictionary<RelationshipAttribute, RelationshipProxy> RelationshipProxies = new Dictionary<RelationshipAttribute, RelationshipProxy>();

        public TraversalHelper(
            IResourceGraph graph,
            IJsonApiContext context)
        {
            _context = context;
            _graph = graph;
        }

        /// <summary>
        /// Creates a root node for breadth-first-traversal. Note that typically, in
        /// JADNC, the root layer will be homogeneous. Also, because it is the first layer,
        /// there can be no relationships to previous layers, only to next layers.
        /// </summary>
        /// <returns>The root node.</returns>
        /// <param name="rootEntities">Root entities.</param>
        /// <typeparam name="TEntity">The 1st type parameter.</typeparam>
        public RootNode<TEntity> CreateRootNode<TEntity>(IEnumerable<TEntity> rootEntities) where TEntity : class, IIdentifiable
        {
            _processedEntities = new Dictionary<DependentType, HashSet<IIdentifiable>>();
            RegisterRelationshipProxies(typeof(TEntity));
            var uniqueEntities = ProcessEntities(rootEntities);
            var relationshipsToNextLayer = GetRelationships(typeof(TEntity));
            return new RootNode<TEntity>(uniqueEntities, relationshipsToNextLayer);
        }

        /// <summary>
        /// Create the first layer after the root layer (based on the root node)
        /// </summary>
        /// <returns>The next layer.</returns>
        /// <param name="rootNode">Root node.</param>
        public EntityChildLayer CreateNextLayer(IEntityNode rootNode)
        {
            return CreateNextLayer(new IEntityNode[] { rootNode });
        }

        /// <summary>
        /// Create a next layer from any previous layer
        /// </summary>
        /// <returns>The next layer.</returns>
        /// <param name="nodes">Nodes.</param>
        public EntityChildLayer CreateNextLayer(IEnumerable<IEntityNode> nodes)
        {
            /// first extract entities by parsing populated relationships in the entities
            /// of previous layer
            (var dependents, var principals) = ExtractEntities(nodes);

            /// group them conveniently so we can make ChildNodes of them
            var dependentsGrouped = GroupByDependentTypeOfRelationship(dependents);

            /// convert the groups into child nodes
            var nextNodes = dependentsGrouped.Select(entry =>
            {
                var nextNodeType = entry.Key;
                var relationshipsToPreviousLayer = entry.Value.Select(grouped =>
                {
                    var proxy = grouped.Key;
                    return CreateRelationsipGroupInstance(nextNodeType, proxy, grouped.Value, principals[proxy]);
                }).ToList();

                RegisterRelationshipProxies(nextNodeType);
                return CreateNodeInstance(nextNodeType, GetRelationships(nextNodeType), relationshipsToPreviousLayer);
            }).ToList();

            /// wrap the child nodes in a EntityChildLayer
            return new EntityChildLayer(nextNodes);
        }


        Dictionary<DependentType, List<KeyValuePair<RelationshipProxy, List<IIdentifiable>>>> GroupByDependentTypeOfRelationship(Dictionary<RelationshipProxy, List<IIdentifiable>> relationships)
        {
            return relationships.GroupBy(kvp => kvp.Key.DependentType).ToDictionary(gdc => gdc.Key, gdc => gdc.ToList());
        }

        /// <summary>
        /// Extracts the entities for the current layer by going through all populated relationships
        /// of the (principal entities of the previous layer.
        /// </summary>
        (Dictionary<RelationshipProxy, List<IIdentifiable>>, Dictionary<RelationshipProxy, List<IIdentifiable>>) ExtractEntities(IEnumerable<IEntityNode> principalNodes)
        {
            var currentLayerEntities = new List<IIdentifiable>();
            var principalsGrouped = new Dictionary<RelationshipProxy, List<IIdentifiable>>();
            var dependentsGrouped = new Dictionary<RelationshipProxy, List<IIdentifiable>>();

            principalNodes.ForEach(n => RegisterRelationshipProxies(n.EntityType));

            foreach (var node in principalNodes)
            {
                var principalEntities = node.UniqueEntities;
                var relationships = node.RelationshipsToNextLayer;
                foreach (IIdentifiable principalEntity in principalEntities)
                {
                    foreach (var proxy in relationships)
                    {
                        var relationshipValue = proxy.GetValue(principalEntity);
                        // skip this relationship if it's not populated
                        if (!proxy.IsContextRelation && relationshipValue == null) continue;
                        if (!(relationshipValue is IEnumerable dependentEntities))
                        {
                            // in the case of a to-one relationship, the assigned value
                            // will not be a list. We therefore first wrap it in a list.
                            var list = TypeHelper.CreateListFor(proxy.DependentType);
                            if (relationshipValue != null) list.Add(relationshipValue);
                            dependentEntities = list;
                        }

                        var newDependentEntities = UniqueInTree(dependentEntities.Cast<IIdentifiable>(), proxy.DependentType);
                        if (proxy.IsContextRelation || newDependentEntities.Any())
                        {
                            currentLayerEntities.AddRange(newDependentEntities);
                            AddToRelationshipGroup(dependentsGrouped, proxy, newDependentEntities); // TODO check if this needs to be newDependentEntities or just dependentEntities
                            AddToRelationshipGroup(principalsGrouped, proxy, new IIdentifiable[] { principalEntity });
                        }
                    }
                }
            }

            var processEntities = GetType().GetMethod(nameof(ProcessEntities), BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var kvp in dependentsGrouped)
            {
                var type = kvp.Key.DependentType;
                var list = kvp.Value.Cast(type);
                processEntities.MakeGenericMethod(type).Invoke(this, new object[] { list });
            }

            return (principalsGrouped, dependentsGrouped);
        }

        /// <summary>
        /// Get all relationships known in the current tree traversal from a 
        /// principal type to any dependent type
        /// </summary>
        /// <returns>The relationships.</returns>
        RelationshipProxy[] GetRelationships(PrincipalType principal)
        {
            return RelationshipProxies.Select(entry => entry.Value).Where(proxy => proxy.PrincipalType == principal).ToArray();
        }

        /// <summary>
        /// Registers the entities as "seen" in the tree traversal, extracts any new <see cref="RelationshipProxy"/>s from it.
        /// </summary>
        /// <returns>The entities.</returns>
        /// <param name="incomingEntities">Incoming entities.</param>
        /// <typeparam name="TEntity">The 1st type parameter.</typeparam>
        HashSet<TEntity> ProcessEntities<TEntity>(IEnumerable<TEntity> incomingEntities) where TEntity : class, IIdentifiable
        {
            Type type = typeof(TEntity);
            var newEntities = UniqueInTree(incomingEntities, type);
            RegisterProcessedEntities(newEntities, type);
            return newEntities;
        }

        void RegisterRelationshipProxies(DependentType type)
        {
            var contextEntity = _graph.GetContextEntity(type);
            foreach (RelationshipAttribute attr in contextEntity.Relationships)
            {
                if (!attr.CanInclude) continue;
                if (!RelationshipProxies.TryGetValue(attr, out RelationshipProxy proxies))
                {
                    DependentType dependentType = GetDependentTypeFromRelationship(attr);
                    var isContextRelation = _context.RelationshipsToUpdate?.ContainsKey(attr);
                    var proxy = new RelationshipProxy(attr, dependentType, isContextRelation != null && (bool)isContextRelation);
                    RelationshipProxies[attr] = proxy;
                }
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
        HashSet<TEntity> UniqueInTree<TEntity>(IEnumerable<TEntity> entities, Type entityType) where TEntity : class, IIdentifiable
        {
            var newEntities = entities.Except(GetProcessedEntities(entityType), ResourceHookExecutor.Comparer).Cast<TEntity>();
            return new HashSet<TEntity>(newEntities);
        }

        /// <summary>
        /// Gets the type from relationship attribute. If the attribute is 
        /// HasManyThrough, and the jointable entity is identifiable, then the target
        /// type is the joinentity instead of the righthand side, because hooks might be 
        /// implemented for the jointable entity.
        /// </summary>
        /// <returns>The target type for traversal</returns>
        /// <param name="attr">Relationship attribute</param>
        DependentType GetDependentTypeFromRelationship(RelationshipAttribute attr)
        {
            if (attr is HasManyThroughAttribute throughAttr && throughAttr.ThroughType.Inherits(typeof(IIdentifiable)))
            {
                return throughAttr.ThroughType;
            }
            return attr.DependentType;
        }

        void AddToRelationshipGroup(Dictionary<RelationshipProxy, List<IIdentifiable>> target, RelationshipProxy proxy, IEnumerable<IIdentifiable> newEntities)
        {
            if (!target.TryGetValue(proxy, out List<IIdentifiable> entities))
            {
                entities = new List<IIdentifiable>();
                target[proxy] = entities;
            }
            entities.AddRange(newEntities);
        }

        /// <summary>
        /// Reflective helper method to create an instance of <see cref="ChildNode{TEntity}"/>;
        /// </summary>
        IEntityNode CreateNodeInstance(DependentType nodeType, RelationshipProxy[] relationshipsToNext, IEnumerable<IRelationshipGroup> relationshipsFromPrev)
        {
            IRelationshipsFromPreviousLayer prev = CreateRelationshipsFromInstance(nodeType, relationshipsFromPrev);
            return (IEntityNode)TypeHelper.CreateInstanceOfOpenType(typeof(ChildNode<>), nodeType, new object[] { relationshipsToNext, prev });
        }

        /// <summary>
        /// Reflective helper method to create an instance of <see cref="RelationshipsFromPreviousLayer{TDependent}"/>;
        /// </summary>
        IRelationshipsFromPreviousLayer CreateRelationshipsFromInstance(DependentType nodeType, IEnumerable<IRelationshipGroup> relationshipsFromPrev)
        {
            var casted = relationshipsFromPrev.Cast(relationshipsFromPrev.First().GetType());
            return (IRelationshipsFromPreviousLayer)TypeHelper.CreateInstanceOfOpenType(typeof(RelationshipsFromPreviousLayer<>), nodeType, new object[] { casted });
        }

        /// <summary>
        /// Reflective helper method to create an instance of <see cref="RelationshipGroup{TDependent}"/>;
        /// </summary>
        IRelationshipGroup CreateRelationsipGroupInstance(Type thisLayerType, RelationshipProxy proxy, List<IIdentifiable> principalEntities, List<IIdentifiable> dependentEntities)
        {
            var dependentEntitiesHashed = TypeHelper.CreateInstanceOfOpenType(typeof(HashSet<>), thisLayerType, dependentEntities.Cast(thisLayerType));
            return (IRelationshipGroup)TypeHelper.CreateInstanceOfOpenType(typeof(RelationshipGroup<>),
                thisLayerType,
                new object[] { proxy, new HashSet<IIdentifiable>(principalEntities), dependentEntitiesHashed });
        }
    }

    /// <summary>
    /// A helper class that represents all entities in the current layer that
    /// are being traversed for which hooks will be executed (see IResourceHookExecutor)
    /// </summary>
    internal class EntityChildLayer : IEnumerable<IEntityNode>
    {
        readonly List<IEntityNode> _collection;

        public bool AnyEntities()
        {
            return _collection.Any(n => n.UniqueEntities.Cast<IIdentifiable>().Any());
        }

        public EntityChildLayer(List<IEntityNode> nodes)
        {
            _collection = nodes;
        }

        public IEnumerator<IEntityNode> GetEnumerator()
        {
            return _collection.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}

