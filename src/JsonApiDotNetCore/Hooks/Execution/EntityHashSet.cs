﻿using System.Collections.Generic;
using JsonApiDotNetCore.Models;
using System.Collections;
using JsonApiDotNetCore.Internal;
using System;

namespace JsonApiDotNetCore.Hooks
{
    /// <summary>
    /// Basically a enumerable of <typeparamref name="TResource"/> of resources that were affected by the request. 
    /// 
    /// Also contains information about updated relationships through 
    /// implementation of IAffectedRelationshipsDictionary<typeparamref name="TResource"/>>
    /// </summary>
    public interface IEntityHashSet<TResource> : IExposeRelationshipsDictionary<TResource>, IEnumerable<TResource> where TResource : class, IIdentifiable { }

    /// <summary>
    /// Implementation of IResourceHashSet{TResource}.
    /// 
    /// Basically a enumerable of <typeparamref name="TResource"/> of resources that were affected by the request. 
    /// 
    /// Also contains information about updated relationships through 
    /// implementation of IRelationshipsDictionary<typeparamref name="TResource"/>>
    /// </summary>
    public class EntityHashSet<TResource> : HashSet<TResource>, IEntityHashSet<TResource> where TResource : class, IIdentifiable
    {
        /// <inheritdoc />
        public RelationshipsDictionary<TResource> AffectedRelationships { get; private set; }

        public EntityHashSet(HashSet<TResource> entities,
                        Dictionary<RelationshipAttribute, HashSet<TResource>> relationships) : base(entities)
        {
            AffectedRelationships = new RelationshipsDictionary<TResource>(relationships);
        }

        /// <summary>
        /// Used internally by the ResourceHookExecutor to make live a bit easier with generics
        /// </summary>
        internal EntityHashSet(IEnumerable entities,
                        Dictionary<RelationshipAttribute, IEnumerable> relationships)
            : this((HashSet<TResource>)entities, TypeHelper.ConvertRelationshipDictionary<TResource>(relationships)) { }


        /// <inheritdoc />
        public Dictionary<RelationshipAttribute, HashSet<TResource>> GetByRelationship(Type principalType)
        {
            return AffectedRelationships.GetByRelationship(principalType);
        }

        /// <inheritdoc />
        public Dictionary<RelationshipAttribute, HashSet<TResource>> GetByRelationship<TPrincipalResource>()  where TPrincipalResource : class, IIdentifiable
        {
            return GetByRelationship<TPrincipalResource>();
        }
    }
}