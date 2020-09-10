using System;
using System.Collections.Generic;
using JsonApiDotNetCore.Queries.Internal.Parsing;

namespace JsonApiDotNetCore.QueryStrings.Internal
{
    public sealed class LegacyFilterNotationConverter
    {
        private const string ParameterNamePrefix = "filter[";
        private const string ParameterNameSuffix = "]";
        private const string OutputParameterName = "filter";

        private const string ExpressionPrefix = "expr:";
        private const string NotEqualsPrefix = "ne:";
        private const string InPrefix = "in:";
        private const string NotInPrefix = "nin:";

        private static readonly Dictionary<string, string> _prefixConversionTable = new Dictionary<string, string>
        {
            ["eq:"] = Keywords.Equals,
            ["lt:"] = Keywords.LessThan,
            ["le:"] = Keywords.LessOrEqual,
            ["gt:"] = Keywords.GreaterThan,
            ["ge:"] = Keywords.GreaterOrEqual,
            ["like:"] = Keywords.Contains
        };

        public (string parameterName, string parameterValue) Convert(string parameterName, string parameterValue)
        {
            if (parameterName == null) throw new ArgumentNullException(nameof(parameterName));
            if (parameterValue == null) throw new ArgumentNullException(nameof(parameterValue));

            if (parameterValue.StartsWith(ExpressionPrefix, StringComparison.Ordinal))
            {
                string expression = parameterValue.Substring(ExpressionPrefix.Length);
                return (parameterName, expression);
            }

            var attributeName = ExtractAttributeName(parameterName);

            foreach (var (prefix, keyword) in _prefixConversionTable)
            {
                if (parameterValue.StartsWith(prefix, StringComparison.Ordinal))
                {
                    var value = parameterValue.Substring(prefix.Length);
                    string escapedValue = EscapeQuotes(value);
                    string expression = $"{keyword}({attributeName},'{escapedValue}')";

                    return (OutputParameterName, expression);
                }
            }

            if (parameterValue.StartsWith(NotEqualsPrefix, StringComparison.Ordinal))
            {
                var value = parameterValue.Substring(NotEqualsPrefix.Length);
                string escapedValue = EscapeQuotes(value);
                string expression = $"{Keywords.Not}({Keywords.Equals}({attributeName},'{escapedValue}'))";

                return (OutputParameterName, expression);
            }

            if (parameterValue.StartsWith(InPrefix, StringComparison.Ordinal))
            {
                string[] valueParts = parameterValue.Substring(InPrefix.Length).Split(",");
                var valueList = "'" + string.Join("','", valueParts) + "'";
                string expression = $"{Keywords.Any}({attributeName},{valueList})";

                return (OutputParameterName, expression);
            }

            if (parameterValue.StartsWith(NotInPrefix, StringComparison.Ordinal))
            {
                string[] valueParts = parameterValue.Substring(NotInPrefix.Length).Split(",");
                var valueList = "'" + string.Join("','", valueParts) + "'";
                string expression = $"{Keywords.Not}({Keywords.Any}({attributeName},{valueList}))";

                return (OutputParameterName, expression);
            }

            if (parameterValue == "isnull:")
            {
                string expression = $"{Keywords.Equals}({attributeName},null)";
                return (OutputParameterName, expression);
            }

            if (parameterValue == "isnotnull:")
            {
                string expression = $"{Keywords.Not}({Keywords.Equals}({attributeName},null))";
                return (OutputParameterName, expression);
            }

            {
                string escapedValue = EscapeQuotes(parameterValue);
                string expression = $"{Keywords.Equals}({attributeName},'{escapedValue}')";

                return (OutputParameterName, expression);
            }
        }

        private static string ExtractAttributeName(string parameterName)
        {
            if (parameterName.StartsWith(ParameterNamePrefix, StringComparison.Ordinal) && parameterName.EndsWith(ParameterNameSuffix, StringComparison.Ordinal))
            {
                string attributeName = parameterName.Substring(ParameterNamePrefix.Length,
                    parameterName.Length - ParameterNamePrefix.Length - ParameterNameSuffix.Length);

                if (attributeName.Length > 0)
                {
                    return attributeName;
                }
            }

            throw new QueryParseException("Expected field name between brackets in filter parameter name.");
        }

        private static string EscapeQuotes(string text)
        {
            return text.Replace("'", "''");
        }
    }
}
