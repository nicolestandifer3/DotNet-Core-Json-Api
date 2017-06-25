using System.Reflection;

namespace JsonApiDotNetCore.Models
{
    public class HasOneAttribute : RelationshipAttribute
    {
        public HasOneAttribute(string publicName, Link documentLinks = Link.All)
        : base(publicName, documentLinks)
        {
            PublicRelationshipName = publicName;
        }

        public override void SetValue(object entity, object newValue)
        {
            var propertyName = (newValue.GetType() == Type) 
                ? InternalRelationshipName 
                : $"{InternalRelationshipName}Id";
                
            var propertyInfo = entity
                .GetType()
                .GetProperty(propertyName);
            
            propertyInfo.SetValue(entity, newValue);
        }
    }
}
