using System;
using System.Collections;
using System.Collections.Generic;
using JsonApiDotNetCore.Extensions;
using JsonApiDotNetCore.Internal;
using JsonApiDotNetCore.Models;
using JsonApiDotNetCore.Models.Annotation;

namespace JsonApiDotNetCore.Hooks
{
    /// <summary>
    /// A class used internally for resource hook execution. Not intended for developer use.
    /// 
    /// A wrapper for RelationshipAttribute with an abstraction layer that works on the 
    /// getters and setters of relationships. These are different in the case of 
    /// HasMany vs HasManyThrough, and HasManyThrough.
    /// It also depends on if the through type (eg ArticleTags)
    /// is identifiable (in which case we will traverse through 
    /// it and fire hooks for it, if defined) or not (in which case we skip 
    /// ArticleTags and go directly to Tags.
    /// </summary>
    internal sealed class RelationshipProxy
    {
        private readonly bool _skipThroughType;

        /// <summary>
        /// The target type for this relationship attribute. 
        /// For HasOne has HasMany this is trivial: just the right-hand side.
        /// For HasManyThrough it is either the ThroughProperty (when the through resource is 
        /// Identifiable) or it is the right-hand side (when the through resource is not identifiable)
        /// </summary>
        public Type RightType { get; }
        public Type LeftType => Attribute.LeftType;
        public bool IsContextRelation { get; }

        public RelationshipAttribute Attribute { get; set; }
        public RelationshipProxy(RelationshipAttribute attr, Type relatedType, bool isContextRelation)
        {
            RightType = relatedType;
            Attribute = attr;
            IsContextRelation = isContextRelation;
            if (attr is HasManyThroughAttribute throughAttr)
            {
                _skipThroughType |= RightType != throughAttr.ThroughType;
            }
        }

        /// <summary>
        /// Gets the relationship value for a given parent resource.
        /// Internally knows how to do this depending on the type of RelationshipAttribute
        /// that this RelationshipProxy encapsulates.
        /// </summary>
        /// <returns>The relationship value.</returns>
        /// <param name="resource">Parent resource.</param>
        public object GetValue(IIdentifiable resource)
        {
            if (Attribute is HasManyThroughAttribute hasManyThrough)
            {
                if (!_skipThroughType)
                {
                    return hasManyThrough.ThroughProperty.GetValue(resource);
                }
                var collection = new List<IIdentifiable>();
                var throughResources = (IEnumerable)hasManyThrough.ThroughProperty.GetValue(resource);
                if (throughResources == null) return null;

                foreach (var throughResource in throughResources)
                {
                    var rightResource = (IIdentifiable)hasManyThrough.RightProperty.GetValue(throughResource);
                    if (rightResource == null) continue;
                    collection.Add(rightResource);
                }

                return collection;
            }
            return Attribute.GetValue(resource);
        }

        /// <summary>
        /// Set the relationship value for a given parent resource.
        /// Internally knows how to do this depending on the type of RelationshipAttribute
        /// that this RelationshipProxy encapsulates.
        /// </summary>
        /// <param name="resource">Parent resource.</param>
        /// <param name="value">The relationship value.</param>
        /// <param name="resourceFactory"></param>
        public void SetValue(IIdentifiable resource, object value, IResourceFactory resourceFactory)
        {
            if (Attribute is HasManyThroughAttribute hasManyThrough)
            {
                if (!_skipThroughType)
                {
                    hasManyThrough.ThroughProperty.SetValue(resource, value);
                    return;
                }

                var throughResources = (IEnumerable)hasManyThrough.ThroughProperty.GetValue(resource);

                var filteredList = new List<object>();
                var rightResources = ((IEnumerable)value).CopyToList(RightType);
                foreach (var throughResource in throughResources)
                {
                    if (((IList)rightResources).Contains(hasManyThrough.RightProperty.GetValue(throughResource)))
                    {
                        filteredList.Add(throughResource);
                    }
                }

                var collectionValue = filteredList.CopyToTypedCollection(hasManyThrough.ThroughProperty.PropertyType);
                hasManyThrough.ThroughProperty.SetValue(resource, collectionValue);
                return;
            }

            Attribute.SetValue(resource, value, resourceFactory);
        }
    }
}
