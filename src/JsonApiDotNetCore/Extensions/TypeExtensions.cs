using JsonApiDotNetCore.Internal;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JsonApiDotNetCore.Models;

namespace JsonApiDotNetCore.Extensions
{
    internal static class TypeExtensions
    {

        /// <summary>
        /// Extension to use the LINQ AddRange method on an IList
        /// </summary>
        public static void AddRange<T>(this IList list, IEnumerable<T> items)
        {
            if (list == null) throw new ArgumentNullException(nameof(list));
            if (items == null) throw new ArgumentNullException(nameof(items));

            if (list is List<T> genericList)
            {
                genericList.AddRange(items);
            }
            else
            {
                foreach (var item in items)
                {
                    list.Add(item);
                }
            }
        }
            
        /// <summary>
        /// Extension to use the LINQ cast method in a non-generic way:
        /// <code>
        /// Type targetType = typeof(TResource)
        /// ((IList)myList).Cast(targetType).
        /// </code>
        /// </summary>
        public static IEnumerable Cast(this IEnumerable source, Type type)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (type == null) throw new ArgumentNullException(nameof(type));
            return TypeHelper.ConvertCollection(source.Cast<object>(), type);
        }

        public static Type GetElementType(this IEnumerable enumerable)
        {
            var enumerableTypes = enumerable.GetType()
                .GetInterfaces()
                .Where(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                .ToList();

            var numberOfEnumerableTypes = enumerableTypes.Count;

            if (numberOfEnumerableTypes == 0)
            {
                throw new ArgumentException($"{nameof(enumerable)} of type {enumerable.GetType().FullName} does not implement a generic variant of {nameof(IEnumerable)}");
            }

            if (numberOfEnumerableTypes > 1)
            {
                throw new ArgumentException($"{nameof(enumerable)} of type {enumerable.GetType().FullName} implements more than one generic variant of {nameof(IEnumerable)}:\n" +
                    $"{string.Join("\n", enumerableTypes.Select(t => t.FullName))}");
            }

            var elementType = enumerableTypes[0].GenericTypeArguments[0];

            return elementType;
        }

        /// <summary>
        /// Creates a List{TInterface} where TInterface is the generic for type specified by t
        /// </summary>
        public static IEnumerable GetEmptyCollection(this Type t)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));

            var listType = typeof(List<>).MakeGenericType(t);
            var list = (IEnumerable)CreateNewInstance(listType);
            return list;
        }

        public static string GetResourceStringId<TResource, TId>(TId id) where TResource : class, IIdentifiable<TId>
        {
            var tempResource = typeof(TResource).New<TResource>();
            tempResource.Id = id;
            return tempResource.StringId;
        }

        public static object New(this Type t)
        {
            return New<object>(t);
        }

        /// <summary>
        /// Creates a new instance of type t, casting it to the specified type.
        /// </summary>
        public static T New<T>(this Type t)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));

            var instance = (T)CreateNewInstance(t);
            return instance;
        }

        private static object CreateNewInstance(Type type)
        {
            try
            {
                return Activator.CreateInstance(type);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException($"Failed to create an instance of '{type.FullName}' using its default constructor.", exception);
            }
        }

        /// <summary>
        /// Whether or not a type implements an interface.
        /// </summary>
        public static bool Implements<T>(this Type concreteType) 
            => Implements(concreteType, typeof(T));

        /// <summary>
        /// Whether or not a type implements an interface.
        /// </summary>
        private static bool Implements(this Type concreteType, Type interfaceType) 
            => interfaceType?.IsAssignableFrom(concreteType) == true;

        /// <summary>
        /// Whether or not a type inherits a base type.
        /// </summary>
        public static bool Inherits<T>(this Type concreteType) 
            => Inherits(concreteType, typeof(T));

        /// <summary>
        /// Whether or not a type inherits a base type.
        /// </summary>
        public static bool Inherits(this Type concreteType, Type interfaceType) 
            => interfaceType?.IsAssignableFrom(concreteType) == true;
    }
}
