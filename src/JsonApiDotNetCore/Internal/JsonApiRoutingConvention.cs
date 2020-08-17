using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Extensions;
using JsonApiDotNetCore.Graph;
using JsonApiDotNetCore.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace JsonApiDotNetCore.Internal
{
    /// <summary>
    /// The default routing convention registers the name of the resource as the route
    /// using the serializer casing convention. The default for this is
    /// a camel case formatter. If the controller directly inherits from <see cref="CoreJsonApiController"/> and there is no
    /// resource directly associated, it uses the name of the controller instead of the name of the type.
    /// </summary>
    /// <example><![CDATA[
    /// public class SomeResourceController : JsonApiController<SomeResource> { } // => /someResources/relationship/relatedResource
    ///
    /// public class RandomNameController<SomeResource> : JsonApiController<SomeResource> { } // => /someResources/relationship/relatedResource
    ///
    /// // when using the kebab-case formatter:
    /// public class SomeResourceController<SomeResource> : JsonApiController<SomeResource> { } // => /some-resources/relationship/related-resource
    ///
    /// public class SomeVeryCustomController<SomeResource> : CoreJsonApiController { } // => /someVeryCustoms/relationship/relatedResource
    /// ]]></example>
    public class JsonApiRoutingConvention : IJsonApiRoutingConvention
    {
        private readonly IJsonApiOptions _options;
        private readonly ResourceNameFormatter _formatter;
        private readonly HashSet<string> _registeredTemplates = new HashSet<string>();
        private readonly Dictionary<string, Type> _registeredResources = new Dictionary<string, Type>();
        
        public JsonApiRoutingConvention(IJsonApiOptions options)
        {
            _options = options;
            _formatter = new ResourceNameFormatter(options);
        }

        /// <inheritdoc/>
        public Type GetAssociatedResource(string controllerName)
        {
            _registeredResources.TryGetValue(controllerName, out Type type);
            return type;
        }

        /// <inheritdoc/>
        public void Apply(ApplicationModel application)
        {
            foreach (var controller in application.Controllers)
            {
                var resourceType = GetResourceTypeFromController(controller.ControllerType);
                
                if (resourceType != null)
                    _registeredResources.Add(controller.ControllerName, resourceType);

                if (RoutingConventionDisabled(controller) == false)
                    continue;

                var template = TemplateFromResource(controller) ?? TemplateFromController(controller);
                if (template == null)
                    throw new JsonApiSetupException($"Controllers with overlapping route templates detected: {controller.ControllerType.FullName}");

                controller.Selectors[0].AttributeRouteModel = new AttributeRouteModel { Template = template };
            }
        }

        /// <summary>
        /// Verifies if routing convention should be enabled for this controller
        /// </summary>
        private bool RoutingConventionDisabled(ControllerModel controller)
        {
            var type = controller.ControllerType;
            var notDisabled = type.GetCustomAttribute<DisableRoutingConventionAttribute>() == null;
            return notDisabled && type.IsSubclassOf(typeof(CoreJsonApiController));
        }

        /// <summary>
        /// Derives a template from the resource type, and checks if this template was already registered.
        /// </summary>
        private string TemplateFromResource(ControllerModel model)
        {
            if (_registeredResources.TryGetValue(model.ControllerName, out Type resourceType))
            {
                var template = $"{_options.Namespace}/{_formatter.FormatResourceName(resourceType)}";
                if (_registeredTemplates.Add(template))
                {
                    return template;
                }
            }
            return null;
        }

        /// <summary>
        /// Derives a template from the controller name, and checks if this template was already registered.
        /// </summary>
        private string TemplateFromController(ControllerModel model)
        {
            string controllerName = _options.SerializerContractResolver.NamingStrategy.GetPropertyName(model.ControllerName, false);

            var template = $"{_options.Namespace}/{controllerName}";
            if (_registeredTemplates.Add(template))
            {
                return template;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Determines the resource associated to a controller by inspecting generic arguments.
        /// </summary>
        private Type GetResourceTypeFromController(Type type)
        {
            var aspNetControllerType = typeof(ControllerBase);
            var coreControllerType = typeof(CoreJsonApiController);
            var baseControllerType = typeof(BaseJsonApiController<,>);
            var currentType = type;
            while (!currentType.IsGenericType || currentType.GetGenericTypeDefinition() != baseControllerType)
            {
                var nextBaseType = currentType.BaseType;

                if ((nextBaseType == aspNetControllerType || nextBaseType == coreControllerType) && currentType.IsGenericType)
                {
                    var resourceType = currentType.GetGenericArguments().FirstOrDefault(t => t.IsOrImplementsInterface(typeof(IIdentifiable)));
                    if (resourceType != null)
                    {
                        return resourceType;
                    }
                }

                currentType = nextBaseType;
                if (nextBaseType == null)
                {
                    break;
                }
            }
            return currentType?.GetGenericArguments().First();
        }
    }
}
