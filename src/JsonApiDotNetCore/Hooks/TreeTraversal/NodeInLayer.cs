﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JsonApiDotNetCore.Internal;
using JsonApiDotNetCore.Models;


namespace JsonApiDotNetCore.Services
{
    /// <summary>
    /// Related entities in current layer entry.
    /// </summary>
    public class NodeInLayer
    {
        private readonly HashSet<IIdentifiable> _uniqueSet;

        public bool IsRootLayerNode { get; private set; }
        public Dictionary<RelationshipProxy, List<IIdentifiable>> RelationshipGroups { get; private set; }
        public Dictionary<RelationshipProxy, List<IIdentifiable>> OriginEntities { get; private set; }
        public List<RelationshipProxy> Relationships { get; private set; }
        public IList UniqueSet { get { return TypeHelper.ConvertCollection(_uniqueSet, EntityType); } }
        public Type EntityType { get; internal set; }

        public NodeInLayer(
            Type principalType,
            HashSet<IIdentifiable> uniqueSet,
            Dictionary<RelationshipProxy, List<IIdentifiable>> entitiesByRelationship,
            Dictionary<RelationshipProxy, List<IIdentifiable>> originEntities,
            List<RelationshipProxy> relationships,
            bool isRootLayerNode
        )
        {
            _uniqueSet = uniqueSet;
            EntityType = principalType;
            RelationshipGroups = entitiesByRelationship;
            OriginEntities = originEntities;
            Relationships = relationships;
            IsRootLayerNode = isRootLayerNode;
        }



        public void UpdateUniqueSet(IEnumerable filteredUniqueSet)
        {
            var casted = new HashSet<IIdentifiable>(filteredUniqueSet.Cast<IIdentifiable>());
            _uniqueSet.IntersectWith(casted);
        }
    }
}

