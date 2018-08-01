using System;
using System.ComponentModel.DataAnnotations.Schema;
using JsonApiDotNetCore.Internal;

namespace JsonApiDotNetCore.Models
{
    public class Identifiable : Identifiable<int>
    { }

    public class Identifiable<T> : IIdentifiable<T>
    {
        /// <summary>
        /// The resource identifier
        /// </summary>
        public virtual T Id { get; set; }

        /// <summary>
        /// The string representation of the `Id`.
        /// 
        /// This is used in serialization and deserialization.
        /// The getters should handle the conversion
        /// from `typeof(T)` to a string and the setter vice versa.
        /// 
        /// To override this behavior, you can either implement the
        /// <see cref="IIdentifiable{T}"> interface directly or override
        /// `GetStringId` and `GetTypedId` methods.
        /// </summary>
        [NotMapped]
        public string StringId
        {
            get => GetStringId(Id);
            set => Id = GetTypedId(value);
        }

        /// <summary>
        /// Convert the provided resource identifier to a string.
        /// </summary>
        protected virtual string GetStringId(object value)
        {
            var type = typeof(T);
            var stringValue = value.ToString();

            if (type == typeof(Guid))
            {
                var guid = Guid.Parse(stringValue);
                return guid == Guid.Empty ? string.Empty : stringValue;
            }

            return stringValue == "0"
                ? string.Empty
                : stringValue;
        }

        /// <summary>
        /// Convert a string to a typed resource identifier.
        /// </summary>
        protected virtual T GetTypedId(string value)
        {
            var convertedValue = TypeHelper.ConvertType(value, typeof(T));
            return convertedValue == null ? default : (T)convertedValue;
        }

        [Obsolete("Use GetTypedId instead")]
        protected virtual object GetConcreteId(string value)
        {
            return TypeHelper.ConvertType(value, typeof(T));
        }
    }
}
