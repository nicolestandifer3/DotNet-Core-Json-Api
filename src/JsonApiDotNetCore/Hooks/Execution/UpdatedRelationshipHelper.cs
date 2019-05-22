﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JsonApiDotNetCore.Models;

namespace JsonApiDotNetCore.Hooks
{
    public interface IUpdatedRelationshipHelper { }

    /// <summary>
    /// A helper class that provides insights in which relationships have been updated for which entities.
    /// </summary>
    public interface IUpdatedRelationshipHelper<TDependent> : IUpdatedRelationshipHelper where TDependent : class, IIdentifiable
    {
        /// <summary>
        /// Gets a dictionary of all entities grouped by affected relationship.
        /// </summary>
        Dictionary<RelationshipAttribute, HashSet<TDependent>> AllEntitiesByRelation { get; }
        /// <summary>
        /// Gets a dictionary of all entities that have an affected relationship to type <typeparamref name="TPrincipal"/>
        /// </summary>
        Dictionary<RelationshipAttribute, HashSet<TDependent>> EntitiesRelatedTo<TPrincipal>() where TPrincipal : class, IIdentifiable;
        /// <summary>
        /// Gets a dictionary of all entities that have an affected relationship to type <paramref name="principalType"/>
        /// </summary>
        Dictionary<RelationshipAttribute, HashSet<TDependent>> EntitiesRelatedTo(Type principalType);
    }

    public class UpdatedRelationshipHelper<TDependent> : IUpdatedRelationshipHelper<TDependent> where TDependent : class, IIdentifiable
    {
        private readonly Dictionary<RelationshipProxy, HashSet<TDependent>> _groups;
        public Dictionary<RelationshipAttribute, HashSet<TDependent>> ImplicitUpdates { get; }

        public Dictionary<RelationshipAttribute, HashSet<TDependent>> AllEntitiesByRelation
        {
            get { return _groups?.ToDictionary(p => p.Key.Attribute, p => p.Value); }
        }
        public UpdatedRelationshipHelper(Dictionary<RelationshipProxy, IEnumerable> relationships)
        {
            _groups = relationships.ToDictionary(kvp => kvp.Key, kvp => new HashSet<TDependent>((IEnumerable<TDependent>)kvp.Value));
        }

        public Dictionary<RelationshipAttribute, HashSet<TDependent>> EntitiesRelatedTo<TPrincipal>() where TPrincipal : class, IIdentifiable
        {
            return EntitiesRelatedTo(typeof(TPrincipal));
        }

        public Dictionary<RelationshipAttribute, HashSet<TDependent>> EntitiesRelatedTo(Type principalType)
        {
            return _groups?.Where(p => p.Key.PrincipalType == principalType).ToDictionary(p => p.Key.Attribute, p => p.Value);
        }
    }
}
