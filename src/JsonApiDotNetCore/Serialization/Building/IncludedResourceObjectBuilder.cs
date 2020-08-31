using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Resources.Annotations;
using JsonApiDotNetCore.Serialization.Objects;

namespace JsonApiDotNetCore.Serialization.Building
{
    public class IncludedResourceObjectBuilder : ResourceObjectBuilder, IIncludedResourceObjectBuilder
    {
        private readonly HashSet<ResourceObject> _included;
        private readonly IFieldsToSerialize _fieldsToSerialize;
        private readonly ILinkBuilder _linkBuilder;

        public IncludedResourceObjectBuilder(IFieldsToSerialize fieldsToSerialize,
                                             ILinkBuilder linkBuilder,
                                             IResourceContextProvider resourceContextProvider,
                                             IResourceObjectBuilderSettingsProvider settingsProvider)
            : base(resourceContextProvider, settingsProvider.Get())
        {
            _included = new HashSet<ResourceObject>(ResourceIdentifierObjectComparer.Instance);
            _fieldsToSerialize = fieldsToSerialize ?? throw new ArgumentNullException(nameof(fieldsToSerialize));
            _linkBuilder = linkBuilder ?? throw new ArgumentNullException(nameof(linkBuilder));
        }

        /// <inheritdoc />
        public IList<ResourceObject> Build()
        {
            if (_included.Any())
            {
                // cleans relationship dictionaries and adds links of resources.
                foreach (var resourceObject in _included)
                {
                    if (resourceObject.Relationships != null)
                    {   // removes relationship entries (<see cref="RelationshipEntry"/>s) if they're completely empty.  
                        var pruned = resourceObject.Relationships.Where(p => p.Value.IsPopulated || p.Value.Links != null).ToDictionary(p => p.Key, p => p.Value);
                        if (!pruned.Any()) pruned = null;
                        resourceObject.Relationships = pruned;
                    }
                    resourceObject.Links = _linkBuilder.GetResourceLinks(resourceObject.Type, resourceObject.Id);
                }
                return _included.ToArray();
            }
            return null;
        }

        /// <inheritdoc />
        public void IncludeRelationshipChain(IReadOnlyCollection<RelationshipAttribute> inclusionChain, IIdentifiable rootResource)
        {
            if (inclusionChain == null) throw new ArgumentNullException(nameof(inclusionChain));
            if (rootResource == null) throw new ArgumentNullException(nameof(rootResource));

            // We don't have to build a resource object for the root resource because
            // this one is already encoded in the documents primary data, so we process the chain
            // starting from the first related resource.
            var relationship = inclusionChain.First();
            var chainRemainder = ShiftChain(inclusionChain);
            var related = relationship.GetValue(rootResource);
            ProcessChain(relationship, related, chainRemainder);
        }

        private void ProcessChain(RelationshipAttribute originRelationship, object related, List<RelationshipAttribute> inclusionChain)
        {
            if (related is IEnumerable children)
                foreach (IIdentifiable child in children)
                    ProcessRelationship(originRelationship, child, inclusionChain);
            else
                ProcessRelationship(originRelationship, (IIdentifiable)related, inclusionChain);
        }

        private void ProcessRelationship(RelationshipAttribute originRelationship, IIdentifiable parent, List<RelationshipAttribute> inclusionChain)
        {
            // get the resource object for parent.
            var resourceObject = GetOrBuildResourceObject(parent, originRelationship);
            if (!inclusionChain.Any())
                return;
            var nextRelationship = inclusionChain.First();
            var chainRemainder = inclusionChain.ToList();
            chainRemainder.RemoveAt(0);

            var nextRelationshipName = nextRelationship.PublicName;
            var relationshipsObject = resourceObject.Relationships;
            // add the relationship entry in the relationship object.
            if (!relationshipsObject.TryGetValue(nextRelationshipName, out var relationshipEntry))
                relationshipsObject[nextRelationshipName] = relationshipEntry = GetRelationshipData(nextRelationship, parent);

            relationshipEntry.Data = GetRelatedResourceLinkage(nextRelationship, parent);

            if (relationshipEntry.HasResource)
            {   // if the relationship is set, continue parsing the chain.
                var related = nextRelationship.GetValue(parent);
                ProcessChain(nextRelationship, related, chainRemainder);
            }
        }

        private List<RelationshipAttribute> ShiftChain(IReadOnlyCollection<RelationshipAttribute> chain)
        {
            var chainRemainder = chain.ToList();
            chainRemainder.RemoveAt(0);
            return chainRemainder;
        }

        /// <summary>
        /// We only need an empty relationship object entry here. It will be populated in the
        /// ProcessRelationships method.
        /// </summary>
        protected override RelationshipEntry GetRelationshipData(RelationshipAttribute relationship, IIdentifiable resource)
        {
            if (relationship == null) throw new ArgumentNullException(nameof(relationship));
            if (resource == null) throw new ArgumentNullException(nameof(resource));

            return new RelationshipEntry { Links = _linkBuilder.GetRelationshipLinks(relationship, resource) };
        }

        /// <summary>
        /// Gets the resource object for <paramref name="parent"/> by searching the included list.
        /// If it was not already built, it is constructed and added to the inclusion list.
        /// </summary>
        private ResourceObject GetOrBuildResourceObject(IIdentifiable parent, RelationshipAttribute relationship)
        {
            var type = parent.GetType();
            var resourceName = ResourceContextProvider.GetResourceContext(type).ResourceName;
            var entry = _included.SingleOrDefault(ro => ro.Type == resourceName && ro.Id == parent.StringId);
            if (entry == null)
            {
                entry = Build(parent, _fieldsToSerialize.GetAttributes(type, relationship), _fieldsToSerialize.GetRelationships(type));
                _included.Add(entry);
            }
            return entry;
        }
    }
}
