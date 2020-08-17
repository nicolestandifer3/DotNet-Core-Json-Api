using System;
using System.Collections.Generic;
using JsonApiDotNetCore.Internal.Contracts;
using JsonApiDotNetCore.Models.Annotation;
using JsonApiDotNetCore.Queries.Expressions;

namespace JsonApiDotNetCore.Internal.Queries.Parsing
{
    public class QueryStringParameterScopeParser : QueryExpressionParser
    {
        private readonly FieldChainRequirements _chainRequirements;
        private readonly Action<ResourceFieldAttribute, ResourceContext, string> _validateSingleFieldCallback;
        private ResourceContext _resourceContextInScope;

        public QueryStringParameterScopeParser(IResourceContextProvider resourceContextProvider, FieldChainRequirements chainRequirements, 
            Action<ResourceFieldAttribute, ResourceContext, string> validateSingleFieldCallback = null)
            : base(resourceContextProvider)
        {
            _chainRequirements = chainRequirements;
            _validateSingleFieldCallback = validateSingleFieldCallback;
        }

        public QueryStringParameterScopeExpression Parse(string source, ResourceContext resourceContextInScope)
        {
            _resourceContextInScope = resourceContextInScope ?? throw new ArgumentNullException(nameof(resourceContextInScope));
            Tokenize(source);

            var expression = ParseQueryStringParameterScope();

            AssertTokenStackIsEmpty();

            return expression;
        }

        protected QueryStringParameterScopeExpression ParseQueryStringParameterScope()
        {
            if (!TokenStack.TryPop(out Token token) || token.Kind != TokenKind.Text)
            {
                throw new QueryParseException("Parameter name expected.");
            }

            var name = new LiteralConstantExpression(token.Value);

            ResourceFieldChainExpression scope = null;

            if (TokenStack.TryPeek(out Token nextToken) && nextToken.Kind == TokenKind.OpenBracket)
            {
                TokenStack.Pop();

                scope = ParseFieldChain(_chainRequirements, null);

                EatSingleCharacterToken(TokenKind.CloseBracket);
            }

            return new QueryStringParameterScopeExpression(name, scope);
        }

        protected override IReadOnlyCollection<ResourceFieldAttribute> OnResolveFieldChain(string path, FieldChainRequirements chainRequirements)
        {
            if (chainRequirements == FieldChainRequirements.EndsInToMany)
            {
                // The mismatch here (ends-in-to-many being interpreted as entire-chain-must-be-to-many) is intentional.
                return ChainResolver.ResolveToManyChain(_resourceContextInScope, path, _validateSingleFieldCallback);
            }

            if (chainRequirements == FieldChainRequirements.IsRelationship)
            {
                return ChainResolver.ResolveRelationshipChain(_resourceContextInScope, path, _validateSingleFieldCallback);
            }

            throw new InvalidOperationException($"Unexpected combination of chain requirement flags '{chainRequirements}'.");
        }
    }
}
