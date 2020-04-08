﻿using System;
using JsonApiDotNetCore.Models;

namespace JsonApiDotNetCore.Internal.Contracts
{
    /// <summary>
    /// Responsible for getting <see cref="ResourceContext"/>s from the <see cref="ResourceGraph"/>.
    /// </summary>
    public interface IResourceContextProvider
    {
        /// <summary>
        /// Gets all registered context entities
        /// </summary>
        ResourceContext[] GetResourceContexts();

        /// <summary>
        /// Get the resource metadata by the DbSet property name
        /// </summary>
        ResourceContext GetResourceContext(string resourceName);

        /// <summary>
        /// Get the resource metadata by the resource type
        /// </summary>
        ResourceContext GetResourceContext(Type resourceType);

        /// <summary>
        /// Get the resource metadata by the resource type
        /// </summary>
        ResourceContext GetResourceContext<TResource>() where TResource : class, IIdentifiable;
    }
}