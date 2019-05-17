﻿using System.Collections.Generic;
using JsonApiDotNetCore.Models;

namespace JsonApiDotNetCore.Hooks
{
    public interface IResourceHookContainer { }

    public interface IResourceHookContainer<T> : IBeforeHooks<T>, IAfterHooks<T>, IOnHooks<T>, IResourceHookContainer where T : class, IIdentifiable { }

    public interface IAfterHooks<T> where T : class, IIdentifiable
    {
        void AfterCreate(HashSet<T> entities, ResourceAction pipeline);
        void AfterRead(HashSet<T> entities, ResourceAction pipeline, bool isRelated = false);
        void AfterUpdate(HashSet<T> entities, ResourceAction pipeline);
        void AfterDelete(HashSet<T> entities, ResourceAction pipeline, bool succeeded);
        void AfterUpdateRelationship(IUpdatedRelationshipHelper<T> relationshipHelper, ResourceAction pipeline);
    }

    public interface IBeforeHooks<T> where T : class, IIdentifiable
    {
        IEnumerable<T> BeforeCreate(HashSet<T> entities, ResourceAction pipeline);
        void BeforeRead(ResourceAction pipeline, bool nestedHook = false, string stringId = null);
        IEnumerable<T> BeforeUpdate(EntityDiff<T> entityDiff, ResourceAction pipeline);
        IEnumerable<T> BeforeDelete(HashSet<T> entities, ResourceAction pipeline);
        IEnumerable<string> BeforeUpdateRelationship(HashSet<string> ids, IUpdatedRelationshipHelper<T> relationshipHelper, ResourceAction pipeline);
        void BeforeImplicitUpdateRelationship(IUpdatedRelationshipHelper<T> relationshipHelper, ResourceAction pipeline);
    }

    public interface IOnHooks<T> where T : class, IIdentifiable
    {
        IEnumerable<T> OnReturn(HashSet<T> entities, ResourceAction pipeline);
    }

    public interface IResourceHookExecutor : IBeforeExecutor, IAfterExecutor, IOnExecutor { }

    public interface IBeforeExecutor
    {
        IEnumerable<TEntity> BeforeCreate<TEntity>(IEnumerable<TEntity> entities, ResourceAction actionSource) where TEntity : class, IIdentifiable;
        void BeforeRead<TEntity>(ResourceAction actionSource, string stringId = null) where TEntity : class, IIdentifiable;
        IEnumerable<TEntity> BeforeUpdate<TEntity>(IEnumerable<TEntity> entities, ResourceAction actionSource) where TEntity : class, IIdentifiable;
        IEnumerable<TEntity> BeforeDelete<TEntity>(IEnumerable<TEntity> entities, ResourceAction actionSource) where TEntity : class, IIdentifiable;
    }

    public interface IAfterExecutor
    {
        void AfterCreate<TEntity>(IEnumerable<TEntity> entities, ResourceAction pipeline) where TEntity : class, IIdentifiable;
        void AfterRead<TEntity>(IEnumerable<TEntity> entities, ResourceAction pipeline) where TEntity : class, IIdentifiable;
        void AfterUpdate<TEntity>(IEnumerable<TEntity> entities, ResourceAction pipeline) where TEntity : class, IIdentifiable;
        void AfterDelete<TEntity>(IEnumerable<TEntity> entities, ResourceAction pipeline, bool succeeded) where TEntity : class, IIdentifiable;
    }

    public interface IOnExecutor
    {
        IEnumerable<TEntity> OnReturn<TEntity>(IEnumerable<TEntity> entities, ResourceAction actionSource) where TEntity : class, IIdentifiable;
    }
}