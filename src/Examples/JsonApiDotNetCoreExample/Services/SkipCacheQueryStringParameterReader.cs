using System.Linq;
using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Exceptions;
using JsonApiDotNetCore.Internal.QueryStrings;
using Microsoft.Extensions.Primitives;

namespace JsonApiDotNetCoreExample.Services
{
    public class SkipCacheQueryStringParameterReader : IQueryStringParameterReader
    {
        private const string _skipCacheParameterName = "skipCache";

        public bool SkipCache { get; private set; }

        public bool IsEnabled(DisableQueryAttribute disableQueryAttribute)
        {
            return !disableQueryAttribute.ParameterNames.Contains(_skipCacheParameterName.ToLowerInvariant());
        }

        public bool CanRead(string parameterName)
        {
            return parameterName == _skipCacheParameterName;
        }

        public void Read(string parameterName, StringValues parameterValue)
        {
            if (!bool.TryParse(parameterValue, out bool skipCache))
            {
                throw new InvalidQueryStringParameterException(parameterName, "Boolean value required.",
                    $"The value {parameterValue} is not a valid boolean.");
            }

            SkipCache = skipCache;
        }
    }
}
