using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries.Expressions;
using JsonApiDotNetCore.Resources.Annotations;

namespace JsonApiDotNetCore.Queries.Internal.Parsing
{
    [PublicAPI]
    public class IncludeParser : QueryExpressionParser
    {
        private readonly Action<RelationshipAttribute, ResourceContext, string> _validateSingleRelationshipCallback;
        private ResourceContext _resourceContextInScope;

        public IncludeParser(IResourceContextProvider resourceContextProvider,
            Action<RelationshipAttribute, ResourceContext, string> validateSingleRelationshipCallback = null)
            : base(resourceContextProvider)
        {
            _validateSingleRelationshipCallback = validateSingleRelationshipCallback;
        }

        public IncludeExpression Parse(string source, ResourceContext resourceContextInScope, int? maximumDepth)
        {
            ArgumentGuard.NotNull(resourceContextInScope, nameof(resourceContextInScope));

            _resourceContextInScope = resourceContextInScope;

            Tokenize(source);

            var expression = ParseInclude(maximumDepth);

            AssertTokenStackIsEmpty();

            return expression;
        }

        protected IncludeExpression ParseInclude(int? maximumDepth)
        {
            ResourceFieldChainExpression firstChain =
                ParseFieldChain(FieldChainRequirements.IsRelationship, "Relationship name expected.");

            var chains = firstChain.AsList();

            while (TokenStack.Any())
            {
                EatSingleCharacterToken(TokenKind.Comma);

                var nextChain = ParseFieldChain(FieldChainRequirements.IsRelationship, "Relationship name expected.");
                chains.Add(nextChain);
            }

            ValidateMaximumIncludeDepth(maximumDepth, chains);

            return IncludeChainConverter.FromRelationshipChains(chains);
        }

        private static void ValidateMaximumIncludeDepth(int? maximumDepth, IReadOnlyCollection<ResourceFieldChainExpression> chains)
        {
            if (maximumDepth != null)
            {
                foreach (var chain in chains)
                {
                    if (chain.Fields.Count > maximumDepth)
                    {
                        var path = string.Join('.', chain.Fields.Select(field => field.PublicName));
                        throw new QueryParseException($"Including '{path}' exceeds the maximum inclusion depth of {maximumDepth}.");
                    }
                }
            }
        }

        protected override IReadOnlyCollection<ResourceFieldAttribute> OnResolveFieldChain(string path, FieldChainRequirements chainRequirements)
        {
            return ChainResolver.ResolveRelationshipChain(_resourceContextInScope, path, _validateSingleRelationshipCallback);
        }
    }
}
