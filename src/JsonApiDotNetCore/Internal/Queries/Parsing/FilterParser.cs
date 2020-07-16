using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Humanizer;
using JsonApiDotNetCore.Internal.Queries.Expressions;
using JsonApiDotNetCore.Models;

namespace JsonApiDotNetCore.Internal.Queries.Parsing
{
    // TODO: Combine callbacks into parsers to make them reusable from ResourceDefinitions.
    public class FilterParser : QueryParser
    {
        private readonly ResolveFieldChainCallback _resolveFieldChainCallback;
        private readonly Func<Type, string, string> _resolveStringId;

        public FilterParser(string source, ResolveFieldChainCallback resolveFieldChainCallback, Func<Type, string, string> resolveStringId)
            : base(source, resolveFieldChainCallback)
        {
            _resolveFieldChainCallback = resolveFieldChainCallback;
            _resolveStringId = resolveStringId ?? throw new ArgumentNullException(nameof(resolveStringId));
        }

        public FilterExpression Parse()
        {
            var expression = ParseFilter();

            AssertTokenStackIsEmpty();

            return expression;
        }

        protected FilterExpression ParseFilter()
        {
            if (TokenStack.TryPeek(out Token nextToken) && nextToken.Kind == TokenKind.Text)
            {
                switch (nextToken.Value)
                {
                    case Keywords.Not:
                    {
                        return ParseNot();
                    }
                    case Keywords.And:
                    case Keywords.Or:
                    {
                        return ParseLogical(nextToken.Value);
                    }
                    case Keywords.Equals:
                    case Keywords.LessThan:
                    case Keywords.LessOrEqual:
                    case Keywords.GreaterThan:
                    case Keywords.GreaterOrEqual:
                    {
                        return ParseComparison(nextToken.Value);
                    }
                    case Keywords.Contains:
                    case Keywords.StartsWith:
                    case Keywords.EndsWith:
                    {
                        return ParseTextMatch(nextToken.Value);
                    }
                    case Keywords.Any:
                    {
                        return ParseAny();
                    }
                    case Keywords.Has:
                    {
                        return ParseHas();
                    }
                }
            }

            throw new QueryParseException("Filter function expected.");
        }

        protected NotExpression ParseNot()
        {
            EatText(Keywords.Not);
            EatSingleCharacterToken(TokenKind.OpenParen);

            FilterExpression child = ParseFilter();

            EatSingleCharacterToken(TokenKind.CloseParen);

            return new NotExpression(child);
        }

        protected LogicalExpression ParseLogical(string operatorName)
        {
            EatText(operatorName);
            EatSingleCharacterToken(TokenKind.OpenParen);

            var terms = new List<QueryExpression>();

            FilterExpression term = ParseFilter();
            terms.Add(term);

            EatSingleCharacterToken(TokenKind.Comma);

            term = ParseFilter();
            terms.Add(term);

            while (TokenStack.TryPeek(out Token nextToken) && nextToken.Kind == TokenKind.Comma)
            {
                EatSingleCharacterToken(TokenKind.Comma);

                term = ParseFilter();
                terms.Add(term);
            }

            EatSingleCharacterToken(TokenKind.CloseParen);

            var logicalOperator = Enum.Parse<LogicalOperator>(operatorName.Pascalize());
            return new LogicalExpression(logicalOperator, terms);
        }

        protected ComparisonExpression ParseComparison(string operatorName)
        {
            var comparisonOperator = Enum.Parse<ComparisonOperator>(operatorName.Pascalize());

            EatText(operatorName);
            EatSingleCharacterToken(TokenKind.OpenParen);

            var leftChainRequirements = comparisonOperator == ComparisonOperator.Equals
                ? FieldChainRequirements.EndsInAttribute | FieldChainRequirements.EndsInToOne
                : FieldChainRequirements.EndsInAttribute;

            QueryExpression leftTerm = ParseCountOrField(leftChainRequirements);

            EatSingleCharacterToken(TokenKind.Comma);

            QueryExpression rightTerm = ParseCountOrConstantOrNullOrField(FieldChainRequirements.EndsInAttribute);

            EatSingleCharacterToken(TokenKind.CloseParen);

            if (leftTerm is ResourceFieldChainExpression leftChain)
            {
                if (leftChainRequirements.HasFlag(FieldChainRequirements.EndsInToOne) &&
                    !(rightTerm is NullConstantExpression))
                {
                    // Run another pass over left chain to have it fail when chain ends in relationship.
                    _resolveFieldChainCallback(leftChain.ToString(), FieldChainRequirements.EndsInAttribute);
                }

                PropertyInfo leftProperty = leftChain.Fields.Last().Property;
                if (leftProperty.Name == nameof(Identifiable.Id) && rightTerm is LiteralConstantExpression rightConstant)
                {
                    string id = _resolveStringId(leftProperty.ReflectedType, rightConstant.Value);
                    rightTerm = new LiteralConstantExpression(id);
                }
            }

            return new ComparisonExpression(comparisonOperator, leftTerm, rightTerm);
        }

        protected MatchTextExpression ParseTextMatch(string matchFunctionName)
        {
            EatText(matchFunctionName);
            EatSingleCharacterToken(TokenKind.OpenParen);

            ResourceFieldChainExpression targetAttribute = ParseFieldChain(FieldChainRequirements.EndsInAttribute, null);

            EatSingleCharacterToken(TokenKind.Comma);

            LiteralConstantExpression constant = ParseConstant();

            EatSingleCharacterToken(TokenKind.CloseParen);

            var matchKind = Enum.Parse<TextMatchKind>(matchFunctionName.Pascalize());
            return new MatchTextExpression(targetAttribute, constant, matchKind);
        }

        protected EqualsAnyOfExpression ParseAny()
        {
            EatText(Keywords.Any);
            EatSingleCharacterToken(TokenKind.OpenParen);

            ResourceFieldChainExpression targetAttribute = ParseFieldChain(FieldChainRequirements.EndsInAttribute, null);

            EatSingleCharacterToken(TokenKind.Comma);

            var constants = new List<LiteralConstantExpression>();

            LiteralConstantExpression constant = ParseConstant();
            constants.Add(constant);

            EatSingleCharacterToken(TokenKind.Comma);

            constant = ParseConstant();
            constants.Add(constant);

            while (TokenStack.TryPeek(out Token nextToken) && nextToken.Kind == TokenKind.Comma)
            {
                EatSingleCharacterToken(TokenKind.Comma);

                constant = ParseConstant();
                constants.Add(constant);
            }

            EatSingleCharacterToken(TokenKind.CloseParen);

            PropertyInfo targetAttributeProperty = targetAttribute.Fields.Last().Property;
            if (targetAttributeProperty.Name == nameof(Identifiable.Id))
            {
                for (int index = 0; index < constants.Count; index++)
                {
                    string stringId = constants[index].Value;
                    string id = _resolveStringId(targetAttributeProperty.ReflectedType, stringId);
                    constants[index] = new LiteralConstantExpression(id);
                }
            }

            return new EqualsAnyOfExpression(targetAttribute, constants);
        }

        protected CollectionNotEmptyExpression ParseHas()
        {
            EatText(Keywords.Has);
            EatSingleCharacterToken(TokenKind.OpenParen);

            ResourceFieldChainExpression targetCollection = ParseFieldChain(FieldChainRequirements.EndsInToMany, null);

            EatSingleCharacterToken(TokenKind.CloseParen);

            return new CollectionNotEmptyExpression(targetCollection);
        }

        protected QueryExpression ParseCountOrField(FieldChainRequirements chainRequirements)
        {
            CountExpression count = TryParseCount();

            if (count != null)
            {
                return count;
            }

            return ParseFieldChain(chainRequirements, "Count function or field name expected.");
        }

        protected QueryExpression ParseCountOrConstantOrNullOrField(FieldChainRequirements chainRequirements)
        {
            CountExpression count = TryParseCount();

            if (count != null)
            {
                return count;
            }

            IdentifierExpression constantOrNull = TryParseConstantOrNull();

            if (constantOrNull != null)
            {
                return constantOrNull;
            }

            return ParseFieldChain(chainRequirements, "Count function, value between quotes, null or field name expected.");
        }

        protected IdentifierExpression TryParseConstantOrNull()
        {
            if (TokenStack.TryPeek(out Token nextToken))
            {
                if (nextToken.Kind == TokenKind.Text && nextToken.Value == Keywords.Null)
                {
                    TokenStack.Pop();
                    return new NullConstantExpression();
                }

                if (nextToken.Kind == TokenKind.QuotedText)
                {
                    TokenStack.Pop();
                    return new LiteralConstantExpression(nextToken.Value);
                }
            }

            return null;
        }

        protected LiteralConstantExpression ParseConstant()
        {
            if (TokenStack.TryPop(out Token token) && token.Kind == TokenKind.QuotedText)
            {
                return new LiteralConstantExpression(token.Value);
            }

            throw new QueryParseException("Value between quotes expected.");
        }
    }
}
