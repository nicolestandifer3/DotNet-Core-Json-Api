using System.Collections.Generic;

namespace JsonApiDotNetCore.Resources
{
    /// <summary>
    /// Compares `IIdentifiable` instances with each other based on StringId.
    /// </summary>
    internal sealed class IdentifiableComparer : IEqualityComparer<IIdentifiable>
    {
        public static readonly IdentifiableComparer Instance = new IdentifiableComparer();

        private IdentifiableComparer()
        {
        }

        public bool Equals(IIdentifiable x, IIdentifiable y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x is null || y is null || x.GetType() != y.GetType())
            {
                return false;
            }

            return x.StringId == y.StringId;
        }

        public int GetHashCode(IIdentifiable obj)
        {
            return obj.StringId != null ? obj.StringId.GetHashCode() : 0;
        }
    }
}
