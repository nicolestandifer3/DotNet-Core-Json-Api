﻿using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JsonApiDotNetCore.Internal;
using JsonApiDotNetCore.Models;
using DependentType = System.Type;

namespace JsonApiDotNetCore.Services
{

    /// <summary>
    /// A helper class that represents all entities in the current layer that
    /// are being traversed for which hooks will be executed (see IResourceHookExecutor)
    /// </summary>
    public class EntityTreeLayer : IEnumerable<NodeInLayer>
    {
        private readonly Dictionary<DependentType, RelationshipGroups> _relationshipGroups;
        private readonly Dictionary<DependentType, HashSet<IIdentifiable>> _uniqueEntities;
        public EntityTreeLayer()
        {
            _relationshipGroups = new Dictionary<DependentType, RelationshipGroups>();
            _uniqueEntities = new Dictionary<DependentType, HashSet<IIdentifiable>>();
        }

        /// <summary>
        /// Gets the unique filtered set.
        /// </summary>
        /// <returns>The unique filtered set.</returns>
        /// <param name="proxy">Proxy.</param>
        public HashSet<IIdentifiable> GetUniqueFilteredSet(RelationshipProxy proxy)
        {
            var match = _uniqueEntities.Where(kvPair => kvPair.Key == proxy.PrincipalType);
            return match.Any() ? match.Single().Value : null;
        }

        /// <summary>
        /// Gets all unique entities.
        /// </summary>
        /// <returns>The all unique entities.</returns>
        public List<IIdentifiable> GetAllUniqueEntities()
        {
            return _uniqueEntities.Values.SelectMany(hs => hs).ToList();
        }

        /// <summary>
        /// Gets all dependent types.
        /// </summary>
        /// <returns>The all dependent types.</returns>
        public List<DependentType> GetAllDependentTypes()
        {
            return _uniqueEntities.Keys.ToList();
        }

        /// <summary>
        /// A boolean that reflects if there are any entities in this layer 
        /// we need to traverse any further for.
        /// </summary>
        /// <returns>A boolean</returns>
        public bool Any()
        {
            return _uniqueEntities.Any();
        }

        /// <summary>
        /// Stores the entities in of the current layer by keeping track of
        /// all the unique entities (for a given dependent type) and keeping
        /// track of the various relationships that are involved with these 
        /// entities, see RelationshipGroups.
        /// </summary>
        /// <param name="relatedEntities">Related entities.</param>
        /// <param name="proxy">Proxy.</param>
        /// <param name="newEntitiesInTree">New entities in tree.</param>
        public void Add(
            IEnumerable<IIdentifiable> relatedEntities,
            RelationshipProxy proxy,
            HashSet<IIdentifiable> newEntitiesInTree
        )
        {
            // the unique set is used to 
            AddToUnique(proxy, newEntitiesInTree);
            AddToRelationshipGroups(proxy, relatedEntities);
        }

        /// <summary>
        /// Entries in this traversal iteration
        /// </summary>
        /// <returns>The entries.</returns>


        private void AddToUnique(RelationshipProxy proxy, HashSet<IIdentifiable> newEntitiesInTree)
        {
            if (!proxy.IsContextRelation && !newEntitiesInTree.Any()) return;
            if (!_uniqueEntities.TryGetValue(proxy.DependentType, out HashSet<IIdentifiable> uniqueSet))
            {
                _uniqueEntities[proxy.DependentType] = newEntitiesInTree;
            }
            else
            {
                uniqueSet.UnionWith(newEntitiesInTree);
            }
        }

        private void AddToRelationshipGroups(RelationshipProxy proxy, IEnumerable<IIdentifiable> relatedEntities)
        {
            var key = proxy.DependentType; 
            if (!_relationshipGroups.TryGetValue(key, out RelationshipGroups groups ))
            {
                groups = new RelationshipGroups(); 
                _relationshipGroups[key] = groups;
            }
            groups.Add(proxy, relatedEntities);
        }

        public IEnumerator<NodeInLayer> GetEnumerator()
        {
            var dependentTypes = _uniqueEntities.Keys;
            foreach (var type in dependentTypes)
            {
                var uniqueEntities = _uniqueEntities[type];
                var relationshipGroups = _relationshipGroups[type].Entries();
                yield return new NodeInLayer(type, uniqueEntities, relationshipGroups);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}

