using System;
using System.Collections.Generic;
using System.Linq;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Exceptions;
using JsonApiDotNetCore.Internal.Contracts;
using JsonApiDotNetCore.Internal.Queries;
using JsonApiDotNetCore.Internal.Queries.Expressions;
using JsonApiDotNetCore.Internal.Queries.Parsing;
using JsonApiDotNetCore.Models.Annotation;
using JsonApiDotNetCore.RequestServices.Contracts;
using Microsoft.Extensions.Primitives;

namespace JsonApiDotNetCore.Internal.QueryStrings
{
    /// <summary>
    /// Reads the 'include' query string parameter and produces a set of query constraints from it.
    /// </summary>
    public interface IIncludeQueryStringParameterReader : IQueryStringParameterReader, IQueryConstraintProvider
    {
    }

    public class IncludeQueryStringParameterReader : QueryStringParameterReader, IIncludeQueryStringParameterReader
    {
        private readonly IJsonApiOptions _options;
        private readonly IncludeParser _includeParser;

        private IncludeExpression _includeExpression;
        private string _lastParameterName;

        public IncludeQueryStringParameterReader(ICurrentRequest currentRequest, IResourceContextProvider resourceContextProvider, IJsonApiOptions options)
            : base(currentRequest, resourceContextProvider)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _includeParser = new IncludeParser(resourceContextProvider, ValidateSingleRelationship);
        }

        private void ValidateSingleRelationship(RelationshipAttribute relationship, ResourceContext resourceContext, string path)
        {
            if (!relationship.CanInclude)
            {
                throw new InvalidQueryStringParameterException(_lastParameterName,
                    "Including the requested relationship is not allowed.",
                    path == relationship.PublicName
                        ? $"Including the relationship '{relationship.PublicName}' on '{resourceContext.ResourceName}' is not allowed."
                        : $"Including the relationship '{relationship.PublicName}' in '{path}' on '{resourceContext.ResourceName}' is not allowed.");
            }
        }

        public bool IsEnabled(DisableQueryAttribute disableQueryAttribute)
        {
            return !disableQueryAttribute.ContainsParameter(StandardQueryStringParameters.Include);
        }

        public bool CanRead(string parameterName)
        {
            return parameterName == "include";
        }

        public void Read(string parameterName, StringValues parameterValue)
        {
            _lastParameterName = parameterName;

            try
            {
                _includeExpression = GetInclude(parameterValue);
            }
            catch (QueryParseException exception)
            {
                throw new InvalidQueryStringParameterException(parameterName, "The specified include is invalid.",
                    exception.Message, exception);
            }
        }

        private IncludeExpression GetInclude(string parameterValue)
        {
            IncludeExpression include = _includeParser.Parse(parameterValue, RequestResource);

            ValidateMaximumIncludeDepth(include);

            return include;
        }

        private void ValidateMaximumIncludeDepth(IncludeExpression include)
        {
            if (_options.MaximumIncludeDepth != null)
            {
                var chains = IncludeChainConverter.GetRelationshipChains(include);

                foreach (var chain in chains)
                {
                    if (chain.Fields.Count > _options.MaximumIncludeDepth)
                    {
                        var path = string.Join('.', chain.Fields.Select(field => field.PublicName));

                        throw new InvalidQueryStringParameterException(_lastParameterName,
                            "Including at the requested depth is not allowed.",
                            $"Including '{path}' exceeds the maximum inclusion depth of {_options.MaximumIncludeDepth}.");
                    }
                }
            }
        }

        public IReadOnlyCollection<ExpressionInScope> GetConstraints()
        {
            var expressionInScope = _includeExpression != null
                ? new ExpressionInScope(null, _includeExpression)
                : new ExpressionInScope(null, IncludeExpression.Empty);

            return new[] {expressionInScope};
        }
    }
}
