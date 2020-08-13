using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using JsonApiDotNetCore.Internal.Contracts;
using JsonApiDotNetCore.Models.Annotation;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Queries.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace JsonApiDotNetCore.Internal.Queries.QueryableBuilding
{
    /// <summary>
    /// Transforms <see cref="SparseFieldSetExpression"/> into <see cref="Queryable.Select{TSource, TKey}(IQueryable{TSource}, Expression{Func{TSource, TKey}})"/> calls.
    /// </summary>
    public class SelectClauseBuilder : QueryClauseBuilder<object>
    {
        private static readonly ConstantExpression _nullConstant = Expression.Constant(null);

        private readonly Expression _source;
        private readonly IModel _entityModel;
        private readonly Type _extensionType;
        private readonly LambdaParameterNameFactory _nameFactory;
        private readonly IResourceFactory _resourceFactory;
        private readonly IResourceContextProvider _resourceContextProvider;

        public SelectClauseBuilder(Expression source, LambdaScope lambdaScope, IModel entityModel, Type extensionType,
            LambdaParameterNameFactory nameFactory, IResourceFactory resourceFactory, IResourceContextProvider resourceContextProvider)
            : base(lambdaScope)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _entityModel = entityModel ?? throw new ArgumentNullException(nameof(entityModel));
            _extensionType = extensionType ?? throw new ArgumentNullException(nameof(extensionType));
            _nameFactory = nameFactory ?? throw new ArgumentNullException(nameof(nameFactory));
            _resourceFactory = resourceFactory ?? throw new ArgumentNullException(nameof(resourceFactory));
            _resourceContextProvider = resourceContextProvider ?? throw new ArgumentNullException(nameof(resourceContextProvider));
        }

        public Expression ApplySelect(IDictionary<ResourceFieldAttribute, QueryLayer> selectors, ResourceContext resourceContext)
        {
            if (selectors == null)
            {
                throw new ArgumentNullException(nameof(selectors));
            }

            if (!selectors.Any())
            {
                return _source;
            }

            Expression bodyInitializer = CreateLambdaBodyInitializer(selectors, resourceContext, LambdaScope, false);

            LambdaExpression lambda = Expression.Lambda(bodyInitializer, LambdaScope.Parameter);

            return SelectExtensionMethodCall(_source, LambdaScope.Parameter.Type, lambda);
        }

        private Expression CreateLambdaBodyInitializer(IDictionary<ResourceFieldAttribute, QueryLayer> selectors, ResourceContext resourceContext,
            LambdaScope lambdaScope, bool lambdaAccessorRequiresTestForNull)
        {
            var propertySelectors = ToPropertySelectors(selectors, resourceContext, lambdaScope.Parameter.Type);
            MemberBinding[] propertyAssignments = propertySelectors.Select(selector => CreatePropertyAssignment(selector, lambdaScope)).Cast<MemberBinding>().ToArray();

            NewExpression newExpression = _resourceFactory.CreateNewExpression(lambdaScope.Accessor.Type);
            Expression memberInit = Expression.MemberInit(newExpression, propertyAssignments);

            if (lambdaScope.HasManyThrough != null)
            {
                MemberBinding outerPropertyAssignment = Expression.Bind(lambdaScope.HasManyThrough.RightProperty, memberInit);

                NewExpression outerNewExpression = _resourceFactory.CreateNewExpression(lambdaScope.HasManyThrough.ThroughType);
                memberInit = Expression.MemberInit(outerNewExpression, outerPropertyAssignment);
            }

            if (!lambdaAccessorRequiresTestForNull)
            {
                return memberInit;
            }

            return TestForNull(lambdaScope.Accessor, memberInit);
        }

        private ICollection<PropertySelector> ToPropertySelectors(IDictionary<ResourceFieldAttribute, QueryLayer> resourceFieldSelectors, 
            ResourceContext resourceContext, Type elementType)
        {
            Dictionary<PropertyInfo, PropertySelector> propertySelectors = new Dictionary<PropertyInfo, PropertySelector>();

            // If a read-only attribute is selected, its value likely depends on another property, so select all resource properties.
            bool includesReadOnlyAttribute = resourceFieldSelectors.Any(selector =>
                selector.Key is AttrAttribute attribute && attribute.Property.SetMethod == null);

            bool containsOnlyRelationships = resourceFieldSelectors.All(selector => selector.Key is RelationshipAttribute);

            foreach (var fieldSelector in resourceFieldSelectors)
            {
                var propertySelector = new PropertySelector(fieldSelector.Key, fieldSelector.Value);
                if (propertySelector.Property.SetMethod != null)
                {
                    propertySelectors[propertySelector.Property] = propertySelector;
                }
            }

            if (includesReadOnlyAttribute || containsOnlyRelationships)
            {
                var entityModel = _entityModel.GetEntityTypes().Single(type => type.ClrType == elementType);
                IEnumerable<IProperty> entityProperties = entityModel.GetProperties().Where(p => !p.IsShadowProperty()).ToArray();

                foreach (var entityProperty in entityProperties)
                {
                    var propertySelector = new PropertySelector(entityProperty.PropertyInfo);
                    if (propertySelector.Property.SetMethod != null)
                    {
                        propertySelectors[propertySelector.Property] = propertySelector;
                    }
                }
            }

            foreach (var eagerLoad in resourceContext.EagerLoads)
            {
                var propertySelector = new PropertySelector(eagerLoad.Property);
                propertySelectors[propertySelector.Property] = propertySelector;
            }

            return propertySelectors.Values;
        }

        private MemberAssignment CreatePropertyAssignment(PropertySelector selector, LambdaScope lambdaScope)
        {
            MemberExpression propertyAccess = Expression.Property(lambdaScope.Accessor, selector.Property);

            Expression assignmentRightHandSide = propertyAccess;
            if (selector.NextLayer != null)
            {
                HasManyThroughAttribute hasManyThrough = selector.OriginatingField as HasManyThroughAttribute;
                var lambdaScopeFactory = new LambdaScopeFactory(_nameFactory, hasManyThrough);

                assignmentRightHandSide = CreateAssignmentRightHandSideForLayer(selector.NextLayer, lambdaScope, propertyAccess,
                    selector.Property, lambdaScopeFactory);
            }

            return Expression.Bind(selector.Property, assignmentRightHandSide);
        }

        private Expression CreateAssignmentRightHandSideForLayer(QueryLayer layer, LambdaScope outerLambdaScope, MemberExpression propertyAccess,
            PropertyInfo selectorPropertyInfo, LambdaScopeFactory lambdaScopeFactory)
        {
            Type collectionElementType = TypeHelper.TryGetCollectionElementType(selectorPropertyInfo.PropertyType);
            Type bodyElementType = collectionElementType ?? selectorPropertyInfo.PropertyType;

            if (collectionElementType != null)
            {
                return CreateCollectionInitializer(outerLambdaScope, selectorPropertyInfo, bodyElementType, layer, lambdaScopeFactory);
            }

            if (layer.Projection == null || !layer.Projection.Any())
            {
                return propertyAccess;
            }

            using var scope = lambdaScopeFactory.CreateScope(bodyElementType, propertyAccess);
            return CreateLambdaBodyInitializer(layer.Projection, layer.ResourceContext, scope, true);
        }

        private Expression CreateCollectionInitializer(LambdaScope lambdaScope, PropertyInfo collectionProperty, 
            Type elementType, QueryLayer layer, LambdaScopeFactory lambdaScopeFactory)
        {
            MemberExpression propertyExpression = Expression.Property(lambdaScope.Accessor, collectionProperty);

            var builder = new QueryableBuilder(propertyExpression, elementType, typeof(Enumerable), _nameFactory,
                _resourceFactory, _resourceContextProvider, _entityModel, lambdaScopeFactory);

            Expression layerExpression = builder.ApplyQuery(layer);

            Type enumerableOfElementType = typeof(IEnumerable<>).MakeGenericType(elementType);
            Type typedCollection = collectionProperty.PropertyType.ToConcreteCollectionType();

            ConstructorInfo typedCollectionConstructor = typedCollection.GetConstructor(new[]
            {
                enumerableOfElementType
            });

            if (typedCollectionConstructor == null)
            {
                throw new Exception(
                    $"Constructor on '{typedCollection.Name}' that accepts '{enumerableOfElementType.Name}' not found.");
            }

            return Expression.New(typedCollectionConstructor, layerExpression);
        }

        private static Expression TestForNull(Expression expressionToTest, Expression ifFalseExpression)
        {
            BinaryExpression equalsNull = Expression.Equal(expressionToTest, _nullConstant);
            return Expression.Condition(equalsNull, Expression.Convert(_nullConstant, expressionToTest.Type), ifFalseExpression);
        }

        private Expression SelectExtensionMethodCall(Expression source, Type elementType, Expression selectorBody)
        {
            return Expression.Call(_extensionType, "Select", new[]
            {
                elementType,
                elementType
            }, source, selectorBody);
        }

        private sealed class PropertySelector
        {
            public PropertyInfo Property { get; }
            public ResourceFieldAttribute OriginatingField { get; }
            public QueryLayer NextLayer { get; }

            public PropertySelector(PropertyInfo property, QueryLayer nextLayer = null)
            {
                Property = property ?? throw new ArgumentNullException(nameof(property));
                NextLayer = nextLayer;
            }

            public PropertySelector(ResourceFieldAttribute field, QueryLayer nextLayer = null)
            {
                OriginatingField = field ?? throw new ArgumentNullException(nameof(field));
                NextLayer = nextLayer;

                Property = field is HasManyThroughAttribute hasManyThrough
                    ? hasManyThrough.ThroughProperty
                    : field.Property;
            }

            public override string ToString()
            {
                return "Property: " + (NextLayer != null ? Property.Name + "..." : Property.Name);
            }
        }
    }
}
