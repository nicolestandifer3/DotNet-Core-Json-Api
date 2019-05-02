﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JsonApiDotNetCore.Internal.Generics;
using JsonApiDotNetCore.Models;
using JsonApiDotNetCore.Services;

namespace JsonApiDotNetCore.Internal
{
    /// <inheritdoc/>
    public class HookExecutorHelper : IHookExecutorHelper
    {

        protected readonly IGenericProcessorFactory _genericProcessorFactory;
        protected readonly IResourceGraph _graph;
        protected readonly Dictionary<Type, IResourceHookContainer> _hookContainers;
        protected readonly Dictionary<Type, IHooksDiscovery> _hookDiscoveries;
        protected readonly List<ResourceHook> _targetedHooksForRelatedEntities;
        protected readonly IJsonApiContext _context;
        protected Dictionary<Type, List<RelationshipProxy>> _meta;

        public HookExecutorHelper(
            IGenericProcessorFactory genericProcessorFactory,
            IResourceGraph graph,
            IJsonApiContext context = null
            )
        {
            _genericProcessorFactory = genericProcessorFactory;
            _graph = graph;
            _context = context;
            _meta = new Dictionary<Type, List<RelationshipProxy>>();
            _hookContainers = new Dictionary<Type, IResourceHookContainer>();
            _hookDiscoveries = new Dictionary<Type, IHooksDiscovery>();
            _targetedHooksForRelatedEntities = new List<ResourceHook>();
        }


        /// <inheritdoc/>
        public IEnumerable<RelationshipProxy> GetMetaEntries(IIdentifiable currentEntityTreeLayerEntity)
        {
            foreach (Type metaKey in _meta.Keys)
            {
                List<RelationshipProxy> proxies = _meta[metaKey];

                foreach (var proxy in proxies)
                {
                    /// because currentEntityTreeLayer is not type-homogeneous (which is 
                    /// why we need to use IIdentifiable for the list type of 
                    /// that layer), we need to check if relatedType is really 
                    /// related to parentType. We do this through comparison of Metakey
                    string identifier = CreateRelationshipIdentifier(proxy.Attribute, currentEntityTreeLayerEntity.GetType());
                    if (proxy.RelationshipIdentifier != identifier) continue;
                    yield return proxy;
                }

            }
        }

        /// <inheritdoc/>
        public IResourceHookContainer GetResourceHookContainer(Type targetEntityType, ResourceHook hook = ResourceHook.None)
        {
            /// checking the cache if we have a reference for the requested container, 
            /// regardless of the hook we will use it for. If the value is null, 
            /// it means there was no implementation IResourceHookContainer at all, 
            /// so we need not even bother.
            if (!_hookContainers.TryGetValue(targetEntityType, out IResourceHookContainer container))
            {
                container = (_genericProcessorFactory.GetProcessor<IResourceHookContainer>(typeof(ResourceDefinition<>), targetEntityType));
                _hookContainers[targetEntityType] = container;
            }
            if (container == null) return container;

            /// if there was a container, first check if it implements the hook we 
            /// want to use it for.
            List<ResourceHook> targetHooks;
            if (hook == ResourceHook.None)
            {
                CheckForTargetHookExistence();
                targetHooks = _targetedHooksForRelatedEntities;
            }
            else
            {
                targetHooks = new List<ResourceHook>() { hook };
            }

            foreach (ResourceHook targetHook in targetHooks)
            {
                if (ShouldExecuteHook(targetEntityType, targetHook)) return container;
            }
            return null;

        }

        public bool ShouldExecuteHook(Type entityType, ResourceHook hook)
        {

            if (!_hookDiscoveries.TryGetValue(entityType, out IHooksDiscovery discovery))
            {
                discovery = _genericProcessorFactory.GetProcessor<IHooksDiscovery>(typeof(IHooksDiscovery<>), entityType);
                _hookDiscoveries[entityType] = discovery;
            }
            return discovery.ImplementedHooks.Contains(hook);
        }

        /// <inheritdoc/>
        public IResourceHookContainer<TEntity> GetResourceHookContainer<TEntity>(ResourceHook hook = ResourceHook.None) where TEntity : class, IIdentifiable
        {
            return (IResourceHookContainer<TEntity>)GetResourceHookContainer(typeof(TEntity), hook);
        }

        /// <inheritdoc/>
        public Dictionary<Type, List<RelationshipProxy>>
            UpdateMetaInformation(
            IEnumerable<Type> nextEntityTreeLayerTypes,
            IEnumerable<ResourceHook> hooks)
        {

            if (hooks == null || !hooks.Any())
            {
                CheckForTargetHookExistence();
                hooks = _targetedHooksForRelatedEntities;
            }
            else
            {
                if (!_targetedHooksForRelatedEntities.Any())
                    _targetedHooksForRelatedEntities.AddRange(hooks);
            }


            foreach (Type parentType in nextEntityTreeLayerTypes)
            {
                var contextEntity = _graph.GetContextEntity(parentType);
                foreach (RelationshipAttribute attr in contextEntity.Relationships)
                {
                    var relatedType = GetTargetTypeFromRelationship(attr);

                    bool hasImplementedHooks = false;
                    foreach (ResourceHook targetHook in hooks)
                    {
                        if (GetResourceHookContainer(relatedType, targetHook) != null)
                        {
                            hasImplementedHooks = true;
                            break;
                        }
                    }
                    /// check for the related type if there are any hooks 
                    /// implemented; if not we can skip to the next relationship
                    if (!hasImplementedHooks) continue;

                    /// If we already have detected relationships for the related 
                    /// type in previous iterations, use that list
                    bool newKey = false;
                    if (!_meta.TryGetValue(relatedType, out List<RelationshipProxy> proxies))
                    {
                        proxies = new List<RelationshipProxy>();
                        newKey = true;
                    }

                    var identifier = CreateRelationshipIdentifier(attr, parentType);

                    var isContextRelation = _context?.RelationshipsToUpdate?.Keys.Contains(attr);
                    var proxy = new RelationshipProxy(attr, relatedType, 
                            parentType, identifier, isContextRelation != null && (bool)isContextRelation);

                    /// we might already have covered for this relationship, like 
                    /// in a hierarchical self-refering nested structure 
                    /// (eg folders).
                    if (!proxies.Select( p => p.RelationshipIdentifier).Contains(proxy.RelationshipIdentifier))
                    {
                        proxies.Add(proxy);
                    }
                    if (newKey && proxies.Any())
                    {
                        _meta[relatedType] = proxies;
                    }
                }
            }
            return _meta;
        }


        /// <inheritdoc/>
        public Dictionary<Type, List<RelationshipProxy>>
            UpdateMetaInformation(
            IEnumerable<Type> nextEntityTreeLayerTypes,
            ResourceHook hook = ResourceHook.None)
        {
            var targetHooks = (hook == ResourceHook.None) ? _targetedHooksForRelatedEntities : new List<ResourceHook> { hook };
            return UpdateMetaInformation(nextEntityTreeLayerTypes, targetHooks);
        }


        /// <summary>
        /// relationship attributes for the same relation are not the same object in memory, for some reason.
        /// This identifier provides a way to compare.
        /// </summary>
        /// <returns>The meta key.</returns>
        /// <param name="attr">Relationship attribute</param>
        /// <param name="parentType">Parent type.</param>
        protected string CreateRelationshipIdentifier(RelationshipAttribute attr, Type parentType)
        {
            string relationType;
            string rightHandIdentifier;
            if (attr is HasManyThroughAttribute manyThroughAttr)
            {
                relationType = "has-many-through";
                rightHandIdentifier = manyThroughAttr.ThroughProperty.Name;
            }
            else if (attr is HasManyAttribute manyAttr)
            {
                relationType = "has-many";
                rightHandIdentifier = manyAttr.RelationshipPath;
            }
            else
            {
                relationType = "has-one";
                rightHandIdentifier = attr.RelationshipPath;
            }
            return $"{parentType.Name} {relationType} {rightHandIdentifier}";
        }

        /// <summary>
        /// Gets the type from relationship attribute. If the attribute is 
        /// HasManyThrough, and the jointable entity is identifiable, then the target
        /// type is the joinentity instead of the righthand side, because hooks might be 
        /// implemented for the jointable entity.
        /// </summary>
        /// <returns>The target type for traversal</returns>
        /// <param name="attr">Relationship attribute</param>
        protected Type GetTargetTypeFromRelationship(RelationshipAttribute attr)
        {
            if (attr is HasManyThroughAttribute throughAttr)
            {
                if (typeof(IIdentifiable).IsAssignableFrom(throughAttr.ThroughType))
                {
                    return throughAttr.ThroughType;
                }
                return attr.Type;
            }
            return attr.Type;
        }

        protected void CheckForTargetHookExistence()
        {
            if (!_targetedHooksForRelatedEntities.Any())
                throw new InvalidOperationException("Something is not right in the breadth first traversal of resource hook: " +
                    "trying to get meta information when no allowed hooks are set");
        }

        public bool RequiresDatabaseDiff(IResourceHookContainer container, ResourceHook hook)
        {
            throw new NotImplementedException();
        }
    }


    /// <summary>
    /// Internal class used for resource hook execution. Not intended for developer use.
    /// 
    /// A wrapper for RelationshipAttribute with an abstraction layer that works on the 
    /// getters and setters of relationships. These are different in the case of 
    /// HasMany vs HasManyThrough, and HasManyThrough.
    /// It also depends on if the jointable entity
    /// (eg ArticleTags) is identifiable (in which case we will traverse through 
    /// it and fire hooks for it, if defined) or not (in which case we skip 
    /// ArticleTags and go directly to Tags.
    /// </summary>
    public class RelationshipProxy
    {
        readonly bool _isHasManyThrough;
        readonly bool _skipJoinTable;

        /// <summary>
        /// The target type for this relationship attribute. 
        /// For HasOne has HasMany this is trivial: just the righthand side.
        /// For HasManyThrough it is either the ThroughProperty (when the jointable is 
        /// Identifiable) or it is the righthand side (when the jointable is not identifiable)
        /// </summary>
        public Type DependentType { get; private set; }
        public Type PrincipalType { get; private set; }
        public string RelationshipIdentifier { get; private set; }
        public bool IsContextRelation { get; private set; }

        public RelationshipAttribute Attribute { get; set; }
        public RelationshipProxy(RelationshipAttribute attr, Type relatedType, 
            Type parentType, string identifier, bool isContextRelation)
        {
            RelationshipIdentifier = identifier;
            PrincipalType = parentType;
            DependentType = relatedType;
            Attribute = attr;
            IsContextRelation = isContextRelation;
            if (attr is HasManyThroughAttribute throughAttr)
            {
                _isHasManyThrough = true;
                _skipJoinTable |= DependentType != throughAttr.ThroughType;
            }
        }


        /// <summary>
        /// Gets the relationship value for a given parent entity.
        /// Internally knows how to do this depending on the type of RelationshipAttribute
        /// that this RelationshipProxy encapsulates.
        /// </summary>
        /// <returns>The relationship value.</returns>
        /// <param name="entity">Parent entity.</param>
        public object GetValue(IIdentifiable entity)
        {
            if (_isHasManyThrough)
            {
                var throughAttr = (HasManyThroughAttribute)Attribute;
                if (!_skipJoinTable)
                {
                    return throughAttr.ThroughProperty.GetValue(entity);
                }
                else
                {
                    var collection = new List<IIdentifiable>();
                    var joinEntities = (IList)throughAttr.ThroughProperty.GetValue(entity);
                    if (joinEntities == null) return null;

                    foreach (var joinEntity in joinEntities)
                    {
                        var rightEntity = (IIdentifiable)throughAttr.RightProperty.GetValue(joinEntity);
                        if (rightEntity == null) continue;
                        collection.Add(rightEntity);
                    }
                    return collection;

                }

            }
            return Attribute.GetValue(entity);
        }

        /// <summary>
        /// Set the relationship value for a given parent entity.
        /// Internally knows how to do this depending on the type of RelationshipAttribute
        /// that this RelationshipProxy encapsulates.
        /// </summary>
        /// <returns>The relationship value.</returns>
        /// <param name="entity">Parent entity.</param>
        public void SetValue(IIdentifiable entity, object value)
        {
            if (_isHasManyThrough)
            {
                if (!_skipJoinTable)
                {
                    var list = (IEnumerable<object>)value;
                    ((HasManyThroughAttribute)Attribute).ThroughProperty.SetValue(entity, TypeHelper.ConvertCollection(list, DependentType));
                    return;
                }
                else
                {
                    var throughAttr = (HasManyThroughAttribute)Attribute;
                    var joinEntities = (IEnumerable<object>)throughAttr.ThroughProperty.GetValue(entity);

                    var filteredList = new List<object>();
                    var rightEntities = TypeHelper.ConvertCollection((IEnumerable<object>)value, DependentType);
                    foreach (var je in joinEntities)
                    {

                        if (rightEntities.Contains(throughAttr.RightProperty.GetValue(je)))
                        {
                            filteredList.Add(je);
                        }
                    }

                    throughAttr.ThroughProperty.SetValue(entity, TypeHelper.ConvertCollection(filteredList, throughAttr.ThroughType));
                    return;
                }

            }
            Attribute.SetValue(entity, value);
        }

    }

}