using System;
using System.Reflection;

namespace JsonApiDotNetCore.Models
{
    public class RelationshipAttribute : Attribute
    {
        protected RelationshipAttribute(string publicName)
        {
            PublicRelationshipName = publicName;
        }

        public string PublicRelationshipName { get; set; }
        public string InternalRelationshipName { get; set; }
        public Type Type { get; set; }
        public bool IsHasMany { get { return this.GetType() == typeof(HasManyAttribute); } }
        public bool IsHasOne { get { return this.GetType() == typeof(HasOneAttribute); } }

        public void SetValue(object entity, object newValue)
        {
            var propertyInfo = entity
                .GetType()
                .GetProperty(InternalRelationshipName);
            
            propertyInfo.SetValue(entity, newValue);        
        }
    }
}
